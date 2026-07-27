using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0010 — Avoid WAITFOR DELAY.</summary>
public sealed class WaitForDelayRule : SqlRuleBase
{
    public override string RuleId => "SQLG0010";
    public override string Title => "Avoid WAITFOR DELAY";
    public override string Description =>
        "WAITFOR DELAY blocks a worker for a fixed time and is rarely appropriate in production T-SQL.";
    public override Severity Severity => Severity.High;
    public override RuleCategory Category => RuleCategory.BestPractices;
    public override IReadOnlyList<string> Tags { get; } = ["waitfor", "delay", "blocking"];

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
        private readonly WaitForDelayRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(WaitForDelayRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(WaitForStatement node)
        {
            if (node.WaitForOption == WaitForOption.Delay)
            {
                _issues.Add(_rule.CreateIssue(
                    _context,
                    node.GetSourceLocation(),
                    "WAITFOR DELAY blocks execution for a fixed duration.",
                    "Prefer event-driven or application-level waits instead of sleeping in T-SQL."));
            }

            base.ExplicitVisit(node);
        }
    }
}
