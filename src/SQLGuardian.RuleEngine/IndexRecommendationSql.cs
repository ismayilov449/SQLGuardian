using System.Text;
using SQLGuardian.Abstractions.Schema;

namespace SQLGuardian.RuleEngine;

/// <summary>Builds safe, idempotent index DDL for recommendations.</summary>
public static class IndexRecommendationSql
{
    public static string CreateNonclusteredIndex(
        string schema,
        string table,
        IReadOnlyList<string> keyColumns,
        string? indexName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentNullException.ThrowIfNull(keyColumns);
        if (keyColumns.Count == 0)
        {
            throw new ArgumentException("At least one key column is required.", nameof(keyColumns));
        }

        var name = indexName ?? $"IX_{table}_{string.Join("_", keyColumns)}";
        var columnList = string.Join(", ", keyColumns.Select(QuoteIdent));
        var qualified = $"{QuoteIdent(schema)}.{QuoteIdent(table)}";
        var quotedName = QuoteIdent(name);

        var sb = new StringBuilder();
        sb.AppendLine($"IF NOT EXISTS (");
        sb.AppendLine($"    SELECT 1");
        sb.AppendLine($"    FROM sys.indexes AS i");
        sb.AppendLine($"    WHERE i.object_id = OBJECT_ID(N'{schema}.{table}')");
        sb.AppendLine($"      AND i.name = N'{EscapeLiteral(name)}'");
        sb.AppendLine($")");
        sb.AppendLine($"BEGIN");
        sb.AppendLine($"    CREATE NONCLUSTERED INDEX {quotedName}");
        sb.AppendLine($"        ON {qualified} ({columnList});");
        sb.AppendLine($"END;");
        return sb.ToString().TrimEnd();
    }

    public static string ExpandSelectList(
        string? schema,
        string table,
        IReadOnlyList<ColumnSchema> columns,
        string? alias = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentNullException.ThrowIfNull(columns);

        if (columns.Count == 0)
        {
            return alias is null ? "*" : $"{alias}.*";
        }

        var prefix = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
        return string.Join(
            ", ",
            columns.Select(c => prefix is null
                ? QuoteIdent(c.Name)
                : $"{prefix}.{QuoteIdent(c.Name)}"));
    }

    private static string QuoteIdent(string name) =>
        "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static string EscapeLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
