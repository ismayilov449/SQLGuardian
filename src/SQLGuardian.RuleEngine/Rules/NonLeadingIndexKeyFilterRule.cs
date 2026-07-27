using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.ScriptDom;
using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>
/// SQLG0022 — Equality filter/join on a column that only appears as a non-leading index key.
/// </summary>
public sealed class NonLeadingIndexKeyFilterRule : SqlRuleBase
{
    public const long DefaultMinRowCount = 1_000;

    public override string RuleId => "SQLG0022";
    public override string Title => "Filter on non-leading index key";
    public override string Description =>
        "Filtering on a column that is only a non-leading index key usually cannot seek that index.";
    public override Severity Severity => Severity.Low;
    public override RuleCategory Category => RuleCategory.Indexing;
    public override IReadOnlyList<string> Tags { get; } = ["schema", "index", "leading-key"];

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
            Consider(predicate.Left, tables, context, predicate.Location, reported, issues);
            Consider(predicate.Right, tables, context, predicate.Location, reported, issues);
        }

        // Also consider column = literal filters via a light pass.
        fragment.Accept(new LiteralEqualityVisitor(this, context, tables, reported, issues));
    }

    private void Consider(
        ColumnRef column,
        IReadOnlyList<TableOccurrence> tables,
        SqlAnalysisContext context,
        SourceLocation location,
        HashSet<string> reported,
        ICollection<Issue> issues)
    {
        if (string.IsNullOrWhiteSpace(column.ColumnName) || context.Schema is null)
        {
            return;
        }

        var tableOccurrence = ResolveTable(column.Qualifier, tables);
        if (tableOccurrence is null)
        {
            return;
        }

        var table = context.Schema.FindTable(tableOccurrence.Schema, tableOccurrence.Name);
        if (table is null || table.ApproximateRowCount < DefaultMinRowCount)
        {
            return;
        }

        if (!SchemaIndexHelpers.IsOnlyNonLeadingKey(table, column.ColumnName))
        {
            return;
        }

        var key = $"{table.QualifiedName}.{column.ColumnName}";
        if (!reported.Add(key))
        {
            return;
        }

        issues.Add(CreateIssue(
            context,
            location,
            $"Column {table.QualifiedName}.{column.ColumnName} appears only as a non-leading index key " +
            $"(table ~{table.ApproximateRowCount:N0} rows).",
            $"Add a nonclustered index leading with {table.QualifiedName}.{column.ColumnName}.",
            IndexRecommendationSql.CreateNonclusteredIndex(
                table.Schema,
                table.Name,
                [column.ColumnName])));
    }

    private static TableOccurrence? ResolveTable(string? qualifier, IReadOnlyList<TableOccurrence> tables)
    {
        if (string.IsNullOrWhiteSpace(qualifier))
        {
            return tables.Count == 1 ? tables[0] : null;
        }

        return tables.FirstOrDefault(t =>
                   string.Equals(t.Alias, qualifier, StringComparison.OrdinalIgnoreCase))
               ?? tables.FirstOrDefault(t =>
                   string.Equals(t.Name, qualifier, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class LiteralEqualityVisitor : TSqlConcreteFragmentVisitor
    {
        private readonly NonLeadingIndexKeyFilterRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly IReadOnlyList<TableOccurrence> _tables;
        private readonly HashSet<string> _reported;
        private readonly ICollection<Issue> _issues;

        public LiteralEqualityVisitor(
            NonLeadingIndexKeyFilterRule rule,
            SqlAnalysisContext context,
            IReadOnlyList<TableOccurrence> tables,
            HashSet<string> reported,
            ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _tables = tables;
            _reported = reported;
            _issues = issues;
        }

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            if (node.ComparisonType == BooleanComparisonType.Equals)
            {
                TryColumn(node.FirstExpression, node);
                TryColumn(node.SecondExpression, node);
            }

            base.ExplicitVisit(node);
        }

        private void TryColumn(ScalarExpression? expression, TSqlFragment location)
        {
            if (expression is not ColumnReferenceExpression column)
            {
                return;
            }

            var identifiers = column.MultiPartIdentifier?.Identifiers;
            if (identifiers is null || identifiers.Count == 0)
            {
                return;
            }

            var columnName = ScriptDomNaming.IdentifierValue(identifiers[^1]) ?? string.Empty;
            string? qualifier = identifiers.Count >= 2
                ? ScriptDomNaming.IdentifierValue(identifiers[^2])
                : null;

            _rule.Consider(
                new ColumnRef(qualifier, columnName),
                _tables,
                _context,
                location.GetSourceLocation(),
                _reported,
                _issues);
        }
    }
}
