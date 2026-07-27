using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0016 — NOT IN (subquery).</summary>
public sealed class NotInSubqueryRule : SqlRuleBase
{
    public override string RuleId => "SQLG0016";
    public override string Title => "Prefer NOT EXISTS over NOT IN (subquery)";
    public override string Description =>
        "NOT IN (subquery) is NULL-sensitive and often should be rewritten as NOT EXISTS.";
    public override Severity Severity => Severity.Medium;
    public override RuleCategory Category => RuleCategory.Correctness;
    public override IReadOnlyList<string> Tags { get; } = ["not-in", "exists", "null"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        fragment.Accept(new Visitor(this, context, issues));
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly NotInSubqueryRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(NotInSubqueryRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(InPredicate node)
        {
            if (node.NotDefined && node.Subquery is not null)
            {
                _issues.Add(_rule.CreateIssue(
                    _context,
                    node.GetSourceLocation(),
                    "NOT IN (subquery) returns unknown results when the subquery yields NULL.",
                    "Prefer NOT EXISTS (SELECT 1 FROM … WHERE …)."));
            }

            base.ExplicitVisit(node);
        }
    }
}
