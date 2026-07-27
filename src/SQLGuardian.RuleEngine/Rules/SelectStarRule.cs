using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.ScriptDom;
using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0001 — Avoid SELECT *.</summary>
public sealed class SelectStarRule : SqlRuleBase
{
    public override string RuleId => "SQLG0001";
    public override string Title => "Avoid SELECT *";
    public override string Description =>
        "Selecting all columns increases I/O, memory usage, and network traffic.";
    public override Severity Severity => Severity.Medium;
    public override RuleCategory Category => RuleCategory.Performance;
    public override IReadOnlyList<string> Tags { get; } = ["select", "wildcard", "io"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        var visitor = new Visitor(this, context, issues);
        fragment.Accept(visitor);
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly SelectStarRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(SelectStarRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(QuerySpecification node)
        {
            var tables = CollectTables(node);
            foreach (var element in node.SelectElements)
            {
                if (element is not SelectStarExpression star)
                {
                    continue;
                }

                var rewritten = TryRewriteSelectList(star, tables, _context.Schema);
                var suggestion = rewritten is null
                    ? "List only the columns you need."
                    : "Replace SELECT * with an explicit column list (suggested SQL uses catalog columns).";

                _issues.Add(_rule.CreateIssue(
                    _context,
                    star.GetSourceLocation(),
                    "SELECT * expands to all columns and increases I/O and coupling to table shape.",
                    suggestion,
                    rewritten));
            }

            base.ExplicitVisit(node);
        }

        private static List<TableOccurrence> CollectTables(QuerySpecification node)
        {
            if (node.FromClause is null)
            {
                return [];
            }

            var visitor = new TableVisitor();
            node.FromClause.Accept(visitor);
            return visitor.Tables.ToList();
        }

        private static string? TryRewriteSelectList(
            SelectStarExpression star,
            IReadOnlyList<TableOccurrence> tables,
            SchemaSnapshot? schema)
        {
            if (schema is null || tables.Count == 0)
            {
                return null;
            }

            var qualifier = star.Qualifier is null
                ? null
                : ScriptDomNaming.Format(star.Qualifier);

            if (!string.IsNullOrWhiteSpace(qualifier))
            {
                var table = ResolveTable(qualifier, tables, schema);
                if (table is null || table.Columns.Count == 0)
                {
                    return null;
                }

                var list = IndexRecommendationSql.ExpandSelectList(
                    table.Schema,
                    table.Name,
                    table.Columns,
                    qualifier);
                return $"SELECT {list}  -- was: {qualifier}.*";
            }

            if (tables.Count != 1)
            {
                // Multi-table unqualified *: expand each table with alias/name prefix.
                var parts = new List<string>();
                foreach (var occurrence in tables)
                {
                    var table = schema.FindTable(occurrence.Schema, occurrence.Name);
                    if (table is null || table.Columns.Count == 0)
                    {
                        continue;
                    }

                    var alias = occurrence.Alias ?? occurrence.Name;
                    parts.Add(IndexRecommendationSql.ExpandSelectList(
                        table.Schema,
                        table.Name,
                        table.Columns,
                        alias));
                }

                return parts.Count == 0
                    ? null
                    : $"SELECT {string.Join(", ", parts)}  -- was: SELECT *";
            }

            {
                var occurrence = tables[0];
                var table = schema.FindTable(occurrence.Schema, occurrence.Name);
                if (table is null || table.Columns.Count == 0)
                {
                    return null;
                }

                var list = IndexRecommendationSql.ExpandSelectList(
                    table.Schema,
                    table.Name,
                    table.Columns,
                    occurrence.Alias);
                return $"SELECT {list}  -- was: SELECT *";
            }
        }

        private static TableSchema? ResolveTable(
            string qualifier,
            IReadOnlyList<TableOccurrence> tables,
            SchemaSnapshot schema)
        {
            var occurrence = tables.FirstOrDefault(t =>
                                 string.Equals(t.Alias, qualifier, StringComparison.OrdinalIgnoreCase))
                             ?? tables.FirstOrDefault(t =>
                                 string.Equals(t.Name, qualifier, StringComparison.OrdinalIgnoreCase));

            return occurrence is null
                ? null
                : schema.FindTable(occurrence.Schema, occurrence.Name);
        }
    }
}
