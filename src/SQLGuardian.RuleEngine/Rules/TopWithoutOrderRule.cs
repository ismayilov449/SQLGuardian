using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0004 — TOP without ORDER BY.</summary>
public sealed class TopWithoutOrderRule : SqlRuleBase
{
    public override string RuleId => "SQLG0004";
    public override string Title => "TOP without ORDER BY";
    public override string Description =>
        "TOP without ORDER BY returns a non-deterministic set of rows.";
    public override Severity Severity => Severity.Medium;
    public override RuleCategory Category => RuleCategory.Correctness;
    public override IReadOnlyList<string> Tags { get; } = ["top", "order-by", "nondeterministic"];

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
        private readonly TopWithoutOrderRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(TopWithoutOrderRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.TopRowFilter is not null && node.OrderByClause is null)
            {
                _issues.Add(_rule.CreateIssue(
                    _context,
                    node.TopRowFilter.GetSourceLocation(),
                    "TOP without ORDER BY makes which rows are returned non-deterministic.",
                    "Add an ORDER BY clause that defines the intended ranking."));
            }

            base.ExplicitVisit(node);
        }
    }
}
