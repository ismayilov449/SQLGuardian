using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0009 — DISTINCT usage.</summary>
public sealed class DistinctRule : SqlRuleBase
{
    public override string RuleId => "SQLG0009";
    public override string Title => "DISTINCT usage";
    public override string Description =>
        "DISTINCT often hides join cardinality problems and adds sort/hash cost.";
    public override Severity Severity => Severity.Low;
    public override RuleCategory Category => RuleCategory.Performance;
    public override IReadOnlyList<string> Tags { get; } = ["distinct", "duplicate-removal"];

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
        private readonly DistinctRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(DistinctRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.UniqueRowFilter == UniqueRowFilter.Distinct)
            {
                _issues.Add(_rule.CreateIssue(
                    _context,
                    node.GetSourceLocation(),
                    "DISTINCT removes duplicates and may mask incorrect join cardinality.",
                    "Prefer fixing joins or filtering so the projection is unique without DISTINCT."));
            }

            base.ExplicitVisit(node);
        }
    }
}
