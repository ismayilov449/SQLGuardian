using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0007 — Avoid cursors.</summary>
public sealed class CursorRule : SqlRuleBase
{
    public override string RuleId => "SQLG0007";
    public override string Title => "Avoid cursors";
    public override string Description =>
        "Cursors encourage row-by-row processing and are usually slower than set-based SQL.";
    public override Severity Severity => Severity.Medium;
    public override RuleCategory Category => RuleCategory.Performance;
    public override IReadOnlyList<string> Tags { get; } = ["cursor", "row-by-row", "rbar"];

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
        private readonly CursorRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(CursorRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(DeclareCursorStatement node)
        {
            Flag(node, "DECLARE CURSOR");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(OpenCursorStatement node)
        {
            Flag(node, "OPEN CURSOR");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(FetchCursorStatement node)
        {
            Flag(node, "FETCH CURSOR");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CloseCursorStatement node)
        {
            Flag(node, "CLOSE CURSOR");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeallocateCursorStatement node)
        {
            Flag(node, "DEALLOCATE CURSOR");
            base.ExplicitVisit(node);
        }

        private void Flag(TSqlFragment node, string kind) =>
            _issues.Add(_rule.CreateIssue(
                _context,
                node.GetSourceLocation(),
                $"{kind} indicates row-by-row processing.",
                "Prefer set-based INSERT/UPDATE/DELETE or other set operations."));
    }
}
