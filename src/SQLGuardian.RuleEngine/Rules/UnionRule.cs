using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0008 — UNION instead of UNION ALL.</summary>
public sealed class UnionRule : SqlRuleBase
{
    public override string RuleId => "SQLG0008";
    public override string Title => "UNION instead of UNION ALL";
    public override string Description =>
        "UNION removes duplicates and is more expensive than UNION ALL when duplicates are impossible or acceptable.";
    public override Severity Severity => Severity.Low;
    public override RuleCategory Category => RuleCategory.Performance;
    public override IReadOnlyList<string> Tags { get; } = ["union", "distinct", "sort"];

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
        private readonly UnionRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(UnionRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(BinaryQueryExpression node)
        {
            if (node.BinaryQueryExpressionType == BinaryQueryExpressionType.Union && !node.All)
            {
                _issues.Add(_rule.CreateIssue(
                    _context,
                    node.GetSourceLocation(),
                    "UNION removes duplicates (sort/hash). Prefer UNION ALL when duplicates cannot occur or are acceptable.",
                    "Replace UNION with UNION ALL when duplicate elimination is not required.",
                    "-- Change:  ... UNION ...\n-- To:      ... UNION ALL ..."));
            }

            base.ExplicitVisit(node);
        }
    }
}
