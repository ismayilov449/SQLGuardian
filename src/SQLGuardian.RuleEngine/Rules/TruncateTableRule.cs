using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0021 — TRUNCATE TABLE.</summary>
public sealed class TruncateTableRule : SqlRuleBase
{
    public override string RuleId => "SQLG0021";
    public override string Title => "TRUNCATE TABLE usage";
    public override string Description =>
        "TRUNCATE TABLE is destructive and cannot apply a WHERE filter.";
    public override Severity Severity => Severity.High;
    public override RuleCategory Category => RuleCategory.Correctness;
    public override IReadOnlyList<string> Tags { get; } = ["truncate", "ddl", "destructive"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        fragment.Accept(new Visitor(this, context, issues));
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly TruncateTableRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(TruncateTableRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(TruncateTableStatement node)
        {
            _issues.Add(_rule.CreateIssue(
                _context,
                node.GetSourceLocation(),
                "TRUNCATE TABLE removes all rows and cannot be filtered.",
                "Use a filtered DELETE when partial removal is intended, and confirm this is deliberate."));
            base.ExplicitVisit(node);
        }
    }
}
