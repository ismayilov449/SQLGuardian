using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.ScriptDom;
using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>
/// SQLG0011 — Foreign key column(s) without a supporting leading-key index.
/// Requires a <see cref="SchemaSnapshot"/> (catalog metadata + row counts only).
/// </summary>
public sealed class MissingForeignKeyIndexRule : ISqlRule
{
    /// <summary>Skip tiny tables where a missing FK index is rarely worth a warning.</summary>
    public const long DefaultMinRowCount = 1_000;

    public string RuleId => "SQLG0011";
    public string Title => "Foreign key without supporting index";
    public string Description =>
        "Foreign key columns that are not the leading key of an index often cause expensive joins and cascades.";
    public Severity Severity => Severity.Medium;
    public RuleCategory Category => RuleCategory.Performance;
    public IReadOnlyList<string> Tags { get; } = ["schema", "index", "foreign-key", "join"];

    public RuleResult Analyze(SqlAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Schema is null)
        {
            return RuleResult.Empty;
        }

        IReadOnlyList<TableSchema> tables;
        if (IsCatalogTarget(context.FilePath))
        {
            tables = context.Schema.Tables;
        }
        else if (ScriptDomSyntax.TryGetFragment(context, out var fragment) && !context.HasParseErrors)
        {
            tables = ResolveReferencedTables(fragment, context.Schema);
        }
        else
        {
            return RuleResult.Empty;
        }

        var issues = new List<Issue>();
        foreach (var table in tables)
        {
            if (table.ApproximateRowCount < DefaultMinRowCount)
            {
                continue;
            }

            foreach (var fk in table.ForeignKeys)
            {
                if (fk.ParentColumns.Count == 0)
                {
                    continue;
                }

                if (SchemaIndexHelpers.HasLeadingKeyIndex(table, fk.ParentColumns))
                {
                    continue;
                }

                var columnList = string.Join(", ", fk.ParentColumns);
                var indexSql = IndexRecommendationSql.CreateNonclusteredIndex(
                    table.Schema,
                    table.Name,
                    fk.ParentColumns);

                issues.Add(new Issue
                {
                    RuleId = RuleId,
                    Title = Title,
                    Message =
                        $"Foreign key '{fk.Name}' on {table.QualifiedName} ({columnList}) " +
                        $"has no supporting index. Table ~{table.ApproximateRowCount:N0} rows; " +
                        $"references {fk.ReferencedQualifiedName}.",
                    Severity = Severity,
                    Category = Category,
                    Location = SourceLocation.Unknown,
                    Suggestion =
                        $"Add a nonclustered index on ({columnList}) so joins and cascades can seek.",
                    SuggestedSql = indexSql,
                    FilePath = context.FilePath,
                    Tags = Tags
                });
            }
        }

        return RuleResult.FromIssues(issues);
    }

    private static bool IsCatalogTarget(string filePath) =>
        string.Equals(filePath, SchemaAnalysisTargets.Catalog, StringComparison.Ordinal);

    private static IReadOnlyList<TableSchema> ResolveReferencedTables(
        Microsoft.SqlServer.TransactSql.ScriptDom.TSqlFragment fragment,
        SchemaSnapshot schema)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tables = new List<TableSchema>();

        foreach (var occurrence in TableVisitor.Collect(fragment).Tables)
        {
            var table = schema.FindTable(occurrence.Schema, occurrence.Name);
            if (table is null)
            {
                continue;
            }

            var key = SchemaSnapshot.TableKey(table.Schema, table.Name);
            if (seen.Add(key))
            {
                tables.Add(table);
            }
        }

        return tables;
    }
}
