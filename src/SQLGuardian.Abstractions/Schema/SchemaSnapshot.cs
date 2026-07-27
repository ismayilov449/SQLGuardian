namespace SQLGuardian.Abstractions.Schema;

/// <summary>
/// Database catalog metadata used by schema-aware rules.
/// Contains table/column/index/FK definitions and approximate row counts — never row payloads.
/// </summary>
public sealed class SchemaSnapshot
{
    private readonly Dictionary<string, TableSchema> _byKey;

    public SchemaSnapshot(
        string databaseName,
        IReadOnlyList<TableSchema> tables,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentNullException.ThrowIfNull(tables);

        DatabaseName = databaseName;
        Tables = tables;
        CapturedAtUtc = capturedAtUtc;
        _byKey = tables.ToDictionary(
            t => TableKey(t.Schema, t.Name),
            StringComparer.OrdinalIgnoreCase);
    }

    public string DatabaseName { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public IReadOnlyList<TableSchema> Tables { get; }

    public TableSchema? FindTable(string? schema, string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(schema))
        {
            return _byKey.TryGetValue(TableKey(schema, tableName), out var exact)
                ? exact
                : null;
        }

        if (_byKey.TryGetValue(TableKey("dbo", tableName), out var dbo))
        {
            return dbo;
        }

        // Unqualified script references: match unique table name across schemas.
        var matches = Tables
            .Where(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    public static string TableKey(string schema, string table) =>
        $"{schema}.{table}";
}
