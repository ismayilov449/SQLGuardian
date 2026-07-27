using Microsoft.Data.SqlClient;
using SQLGuardian.Abstractions.Schema;

namespace SQLGuardian.Schema;

/// <summary>
/// Reads SQL Server catalog views and partition row estimates.
/// Never issues SELECT against user tables for row payloads.
/// </summary>
public sealed class SqlServerSchemaProvider : ISchemaProvider
{
    private readonly string _connectionString;

    public SqlServerSchemaProvider(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public Task<SchemaSnapshot> LoadAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(SchemaLoadOptions.Full, cancellationToken);

    public async Task<SchemaSnapshot> LoadAsync(
        SchemaLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= SchemaLoadOptions.Full;

        return options.Profile switch
        {
            SchemaLoadProfile.CatalogScan => await LoadCatalogScanAsync(options, cancellationToken)
                .ConfigureAwait(false),
            SchemaLoadProfile.RowCountsOnly => await LoadRowCountsOnlyAsync(options, cancellationToken)
                .ConfigureAwait(false),
            _ => await LoadFullAsync(options, cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<SchemaSnapshot> LoadRowCountsOnlyAsync(
        SchemaLoadOptions options,
        CancellationToken cancellationToken)
    {
        var databaseNameTask = LoadDatabaseNameAsync(cancellationToken);
        var rowCountsTask = WithConnectionAsync(
            options,
            static (c, o, ct) => LoadRowCountsAsync(c, fkParentFilter: null, o, ct),
            cancellationToken);

        await Task.WhenAll(databaseNameTask, rowCountsTask).ConfigureAwait(false);

        return BuildSnapshot(
            await databaseNameTask.ConfigureAwait(false),
            await rowCountsTask.ConfigureAwait(false),
            columns: new Dictionary<string, List<ColumnSchema>>(StringComparer.OrdinalIgnoreCase),
            indexes: new Dictionary<string, List<IndexSchema>>(StringComparer.OrdinalIgnoreCase),
            foreignKeys: new Dictionary<string, List<ForeignKeySchema>>(StringComparer.OrdinalIgnoreCase));
    }

    private async Task<SchemaSnapshot> LoadFullAsync(
        SchemaLoadOptions options,
        CancellationToken cancellationToken)
    {
        var databaseNameTask = LoadDatabaseNameAsync(cancellationToken);
        var rowCountsTask = WithConnectionAsync(
            options,
            static (c, o, ct) => LoadRowCountsAsync(c, fkParentFilter: null, o, ct),
            cancellationToken);
        var columnsTask = WithConnectionAsync(options, LoadColumnsAsync, cancellationToken);
        var indexesTask = WithConnectionAsync(
            options,
            static (c, o, ct) => LoadIndexesAsync(c, fkParentFilter: null, o, ct),
            cancellationToken);
        var foreignKeysTask = WithConnectionAsync(options, LoadForeignKeysAsync, cancellationToken);

        await Task.WhenAll(databaseNameTask, rowCountsTask, columnsTask, indexesTask, foreignKeysTask)
            .ConfigureAwait(false);

        return BuildSnapshot(
            await databaseNameTask.ConfigureAwait(false),
            await rowCountsTask.ConfigureAwait(false),
            await columnsTask.ConfigureAwait(false),
            await indexesTask.ConfigureAwait(false),
            await foreignKeysTask.ConfigureAwait(false));
    }

    private async Task<SchemaSnapshot> LoadCatalogScanAsync(
        SchemaLoadOptions options,
        CancellationToken cancellationToken)
    {
        // Catalog scan only needs FKs + row counts + indexes on FK parents — skip all columns.
        var databaseNameTask = LoadDatabaseNameAsync(cancellationToken);
        var foreignKeysTask = WithConnectionAsync(options, LoadForeignKeysAsync, cancellationToken);

        await Task.WhenAll(databaseNameTask, foreignKeysTask).ConfigureAwait(false);
        var foreignKeys = await foreignKeysTask.ConfigureAwait(false);

        if (foreignKeys.Count == 0)
        {
            return new SchemaSnapshot(
                await databaseNameTask.ConfigureAwait(false),
                [],
                DateTimeOffset.UtcNow);
        }

        var fkParents = foreignKeys.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rowCountsTask = WithConnectionAsync(
            options,
            (c, o, ct) => LoadRowCountsAsync(c, fkParents, o, ct),
            cancellationToken);
        var indexesTask = WithConnectionAsync(
            options,
            (c, o, ct) => LoadIndexesAsync(c, fkParents, o, ct),
            cancellationToken);

        await Task.WhenAll(rowCountsTask, indexesTask).ConfigureAwait(false);

        return BuildSnapshot(
            await databaseNameTask.ConfigureAwait(false),
            await rowCountsTask.ConfigureAwait(false),
            columns: new Dictionary<string, List<ColumnSchema>>(StringComparer.OrdinalIgnoreCase),
            await indexesTask.ConfigureAwait(false),
            foreignKeys);
    }

    private async Task<string> LoadDatabaseNameAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(connection.Database))
        {
            return connection.Database;
        }

        return await ScalarStringAsync(connection, "SELECT DB_NAME();", 30, cancellationToken)
                   .ConfigureAwait(false)
               ?? "unknown";
    }

    private async Task<T> WithConnectionAsync<T>(
        SchemaLoadOptions options,
        Func<SqlConnection, SchemaLoadOptions, CancellationToken, Task<T>> loader,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await loader(connection, options, cancellationToken).ConfigureAwait(false);
    }

    private static SchemaSnapshot BuildSnapshot(
        string databaseName,
        Dictionary<string, long> rowCounts,
        Dictionary<string, List<ColumnSchema>> columns,
        Dictionary<string, List<IndexSchema>> indexes,
        Dictionary<string, List<ForeignKeySchema>> foreignKeys)
    {
        var tableKeys = rowCounts.Keys
            .Concat(columns.Keys)
            .Concat(indexes.Keys)
            .Concat(foreignKeys.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tables = new List<TableSchema>(tableKeys.Count);
        foreach (var key in tableKeys)
        {
            var (schema, name) = SplitKey(key);
            tables.Add(new TableSchema
            {
                Schema = schema,
                Name = name,
                ApproximateRowCount = rowCounts.GetValueOrDefault(key),
                Columns = columns.GetValueOrDefault(key) ?? [],
                Indexes = indexes.GetValueOrDefault(key) ?? [],
                ForeignKeys = foreignKeys.GetValueOrDefault(key) ?? []
            });
        }

        return new SchemaSnapshot(databaseName, tables, DateTimeOffset.UtcNow);
    }

    private static async Task<Dictionary<string, long>> LoadRowCountsAsync(
        SqlConnection connection,
        HashSet<string>? fkParentFilter,
        SchemaLoadOptions options,
        CancellationToken cancellationToken)
    {
        var sql = fkParentFilter is null
            ? """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                SUM(CAST(p.rows AS bigint)) AS ApproximateRows
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.partitions AS p
                ON p.object_id = t.object_id
               AND p.index_id IN (0, 1)
            WHERE t.is_ms_shipped = 0
            GROUP BY s.name, t.name;
            """
            : """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                SUM(CAST(p.rows AS bigint)) AS ApproximateRows
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.partitions AS p
                ON p.object_id = t.object_id
               AND p.index_id IN (0, 1)
            WHERE t.is_ms_shipped = 0
              AND EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys AS fk
                    WHERE fk.parent_object_id = t.object_id)
            GROUP BY s.name, t.name;
            """;

        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await using var command = CreateCommand(connection, sql, options);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = SchemaSnapshot.TableKey(reader.GetString(0), reader.GetString(1));
            if (fkParentFilter is not null && !fkParentFilter.Contains(key))
            {
                continue;
            }

            result[key] = reader.GetInt64(2);
        }

        return result;
    }

    private static async Task<Dictionary<string, List<ColumnSchema>>> LoadColumnsAsync(
        SqlConnection connection,
        SchemaLoadOptions options,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                c.name AS ColumnName,
                c.column_id AS Ordinal,
                c.is_nullable AS IsNullable,
                ty.name AS DataType
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.columns AS c ON c.object_id = t.object_id
            INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name, c.column_id;
            """;

        var result = new Dictionary<string, List<ColumnSchema>>(StringComparer.OrdinalIgnoreCase);
        await using var command = CreateCommand(connection, sql, options);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = SchemaSnapshot.TableKey(reader.GetString(0), reader.GetString(1));
            if (!result.TryGetValue(key, out var list))
            {
                list = [];
                result[key] = list;
            }

            list.Add(new ColumnSchema
            {
                Name = reader.GetString(2),
                Ordinal = reader.GetInt32(3),
                IsNullable = reader.GetBoolean(4),
                DataType = reader.GetString(5)
            });
        }

        return result;
    }

    private static async Task<Dictionary<string, List<IndexSchema>>> LoadIndexesAsync(
        SqlConnection connection,
        HashSet<string>? fkParentFilter,
        SchemaLoadOptions options,
        CancellationToken cancellationToken)
    {
        // When filtering to FK parents, push the filter into SQL so index_columns stays small.
        var sql = fkParentFilter is null
            ? """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                i.name AS IndexName,
                i.is_unique AS IsUnique,
                i.is_primary_key AS IsPrimaryKey,
                i.is_unique_constraint AS IsUniqueConstraint,
                c.name AS ColumnName,
                ic.key_ordinal AS KeyOrdinal,
                ic.is_included_column AS IsIncluded
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.indexes AS i ON i.object_id = t.object_id
            INNER JOIN sys.index_columns AS ic
                ON ic.object_id = i.object_id
               AND ic.index_id = i.index_id
            INNER JOIN sys.columns AS c
                ON c.object_id = ic.object_id
               AND c.column_id = ic.column_id
            WHERE t.is_ms_shipped = 0
              AND i.type > 0
              AND i.name IS NOT NULL
            ORDER BY s.name, t.name, i.name, ic.is_included_column, ic.key_ordinal, ic.index_column_id;
            """
            : """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                i.name AS IndexName,
                i.is_unique AS IsUnique,
                i.is_primary_key AS IsPrimaryKey,
                i.is_unique_constraint AS IsUniqueConstraint,
                c.name AS ColumnName,
                ic.key_ordinal AS KeyOrdinal,
                ic.is_included_column AS IsIncluded
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.indexes AS i ON i.object_id = t.object_id
            INNER JOIN sys.index_columns AS ic
                ON ic.object_id = i.object_id
               AND ic.index_id = i.index_id
            INNER JOIN sys.columns AS c
                ON c.object_id = ic.object_id
               AND c.column_id = ic.column_id
            WHERE t.is_ms_shipped = 0
              AND i.type > 0
              AND i.name IS NOT NULL
              AND EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys AS fk
                    WHERE fk.parent_object_id = t.object_id)
            ORDER BY s.name, t.name, i.name, ic.is_included_column, ic.key_ordinal, ic.index_column_id;
            """;

        var builders = new Dictionary<string, Dictionary<string, IndexBuilder>>(StringComparer.OrdinalIgnoreCase);
        await using var command = CreateCommand(connection, sql, options);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var tableKey = SchemaSnapshot.TableKey(reader.GetString(0), reader.GetString(1));
            if (fkParentFilter is not null && !fkParentFilter.Contains(tableKey))
            {
                continue;
            }

            if (!builders.TryGetValue(tableKey, out var byIndex))
            {
                byIndex = new Dictionary<string, IndexBuilder>(StringComparer.OrdinalIgnoreCase);
                builders[tableKey] = byIndex;
            }

            var indexName = reader.GetString(2);
            if (!byIndex.TryGetValue(indexName, out var builder))
            {
                builder = new IndexBuilder(
                    indexName,
                    reader.GetBoolean(3),
                    reader.GetBoolean(4),
                    reader.GetBoolean(5));
                byIndex[indexName] = builder;
            }

            var columnName = reader.GetString(6);
            var isIncluded = reader.GetBoolean(8);
            if (isIncluded)
            {
                builder.Included.Add(columnName);
            }
            else
            {
                builder.Keys.Add(columnName);
            }
        }

        var result = new Dictionary<string, List<IndexSchema>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tableKey, byIndex) in builders)
        {
            result[tableKey] = byIndex.Values
                .Select(b => b.ToSchema())
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return result;
    }

    private static async Task<Dictionary<string, List<ForeignKeySchema>>> LoadForeignKeysAsync(
        SqlConnection connection,
        SchemaLoadOptions options,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                OBJECT_SCHEMA_NAME(fk.parent_object_id) AS ParentSchema,
                OBJECT_NAME(fk.parent_object_id) AS ParentTable,
                fk.name AS ForeignKeyName,
                pc.name AS ParentColumn,
                OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS ReferencedSchema,
                OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
                rc.name AS ReferencedColumn,
                fkc.constraint_column_id AS ColumnOrdinal
            FROM sys.foreign_keys AS fk
            INNER JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.columns AS pc
                ON pc.object_id = fkc.parent_object_id
               AND pc.column_id = fkc.parent_column_id
            INNER JOIN sys.columns AS rc
                ON rc.object_id = fkc.referenced_object_id
               AND rc.column_id = fkc.referenced_column_id
            WHERE OBJECTPROPERTY(fk.parent_object_id, 'IsMSShipped') = 0
            ORDER BY ParentSchema, ParentTable, ForeignKeyName, ColumnOrdinal;
            """;

        var builders = new Dictionary<string, Dictionary<string, FkBuilder>>(StringComparer.OrdinalIgnoreCase);
        await using var command = CreateCommand(connection, sql, options);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var parentSchema = reader.GetString(0);
            var parentTable = reader.GetString(1);
            var tableKey = SchemaSnapshot.TableKey(parentSchema, parentTable);
            if (!builders.TryGetValue(tableKey, out var byFk))
            {
                byFk = new Dictionary<string, FkBuilder>(StringComparer.OrdinalIgnoreCase);
                builders[tableKey] = byFk;
            }

            var fkName = reader.GetString(2);
            if (!byFk.TryGetValue(fkName, out var builder))
            {
                builder = new FkBuilder(
                    fkName,
                    parentSchema,
                    parentTable,
                    reader.GetString(4),
                    reader.GetString(5));
                byFk[fkName] = builder;
            }

            builder.ParentColumns.Add(reader.GetString(3));
            builder.ReferencedColumns.Add(reader.GetString(6));
        }

        var result = new Dictionary<string, List<ForeignKeySchema>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tableKey, byFk) in builders)
        {
            result[tableKey] = byFk.Values
                .Select(b => b.ToSchema())
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return result;
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string sql, SchemaLoadOptions options)
    {
        var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = Math.Max(5, options.CommandTimeoutSeconds)
        };
        return command;
    }

    private static async Task<string?> ScalarStringAsync(
        SqlConnection connection,
        string sql,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = timeoutSeconds };
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value as string;
    }

    private static (string Schema, string Name) SplitKey(string key)
    {
        var dot = key.IndexOf('.');
        if (dot <= 0 || dot >= key.Length - 1)
        {
            return ("dbo", key);
        }

        return (key[..dot], key[(dot + 1)..]);
    }

    private sealed class IndexBuilder(
        string name,
        bool isUnique,
        bool isPrimaryKey,
        bool isUniqueConstraint)
    {
        public List<string> Keys { get; } = [];
        public List<string> Included { get; } = [];

        public IndexSchema ToSchema() => new()
        {
            Name = name,
            IsUnique = isUnique,
            IsPrimaryKey = isPrimaryKey,
            IsUniqueConstraint = isUniqueConstraint,
            KeyColumns = Keys,
            IncludedColumns = Included
        };
    }

    private sealed class FkBuilder(
        string name,
        string parentSchema,
        string parentTable,
        string referencedSchema,
        string referencedTable)
    {
        public List<string> ParentColumns { get; } = [];
        public List<string> ReferencedColumns { get; } = [];

        public ForeignKeySchema ToSchema() => new()
        {
            Name = name,
            ParentSchema = parentSchema,
            ParentTable = parentTable,
            ParentColumns = ParentColumns,
            ReferencedSchema = referencedSchema,
            ReferencedTable = referencedTable,
            ReferencedColumns = ReferencedColumns
        };
    }
}
