using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>
/// SQLG0012 — Equality join/filter column without a supporting leading-key index.
/// Requires a <see cref="SchemaSnapshot"/>.
/// </summary>
public sealed class UnindexedJoinColumnRule : SqlRuleBase
{
    public const long DefaultMinRowCount = 1_000;

    public override string RuleId => "SQLG0012";
    public override string Title => "Unindexed join column";
    public override string Description =>
        "Columns used in equality joins that are not leading index keys can force scans on larger tables.";
    public override Severity Severity => Severity.Medium;
    public override RuleCategory Category => RuleCategory.Performance;
    public override IReadOnlyList<string> Tags { get; } = ["schema", "index", "join"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        if (context.Schema is null)
        {
            return;
        }

        var tables = TableVisitor.Collect(fragment).Tables;
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var predicate in EqualityColumnPredicateVisitor.Collect(fragment).Predicates)
        {
            ConsiderSide(predicate.Left, tables, context, predicate.Location, reported, issues);
            ConsiderSide(predicate.Right, tables, context, predicate.Location, reported, issues);
        }
    }

    private void ConsiderSide(
        ColumnRef column,
        IReadOnlyList<TableOccurrence> tables,
        SqlAnalysisContext context,
        SourceLocation location,
        HashSet<string> reported,
        ICollection<Issue> issues)
    {
        if (string.IsNullOrWhiteSpace(column.ColumnName))
        {
            return;
        }

        var tableOccurrence = ResolveTable(column.Qualifier, tables);
        if (tableOccurrence is null || context.Schema is null)
        {
            return;
        }

        var table = context.Schema.FindTable(tableOccurrence.Schema, tableOccurrence.Name);
        if (table is null || table.ApproximateRowCount < DefaultMinRowCount)
        {
            return;
        }

        var columns = new[] { column.ColumnName };
        if (SchemaIndexHelpers.HasLeadingKeyIndex(table, columns))
        {
            return;
        }

        // PK leading keys are already covered by HasLeadingKeyIndex; extra guard for clarity.
        if (SchemaIndexHelpers.IsLeadingKeyOfPrimaryKey(table, columns))
        {
            return;
        }

        var dedupeKey = $"{table.QualifiedName}.{column.ColumnName}";
        if (!reported.Add(dedupeKey))
        {
            return;
        }

        issues.Add(CreateIssue(
            context,
            location,
            $"Column {table.QualifiedName}.{column.ColumnName} is used in an equality predicate " +
            $"but is not a leading index key. Table ~{table.ApproximateRowCount:N0} rows.",
            $"Add a nonclustered index so this join/filter can seek on {table.QualifiedName}.{column.ColumnName}.",
            IndexRecommendationSql.CreateNonclusteredIndex(
                table.Schema,
                table.Name,
                columns)));
    }

    private static TableOccurrence? ResolveTable(string? qualifier, IReadOnlyList<TableOccurrence> tables)
    {
        if (string.IsNullOrWhiteSpace(qualifier))
        {
            return tables.Count == 1 ? tables[0] : null;
        }

        var byAlias = tables.FirstOrDefault(t =>
            string.Equals(t.Alias, qualifier, StringComparison.OrdinalIgnoreCase));
        if (byAlias is not null)
        {
            return byAlias;
        }

        return tables.FirstOrDefault(t =>
            string.Equals(t.Name, qualifier, StringComparison.OrdinalIgnoreCase));
    }
}
