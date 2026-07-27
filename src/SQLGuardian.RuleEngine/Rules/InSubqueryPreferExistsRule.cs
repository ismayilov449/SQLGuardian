using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0017 — IN (subquery) prefer EXISTS.</summary>
public sealed class InSubqueryPreferExistsRule : SqlRuleBase
{
    public override string RuleId => "SQLG0017";
    public override string Title => "Prefer EXISTS over IN (subquery)";
    public override string Description =>
        "col IN (SELECT …) is often clearer as EXISTS for semi-join / existence checks.";
    public override Severity Severity => Severity.Low;
    public override RuleCategory Category => RuleCategory.Performance;
    public override IReadOnlyList<string> Tags { get; } = ["in", "exists", "subquery"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        fragment.Accept(new Visitor(this, context, issues));
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly InSubqueryPreferExistsRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(InSubqueryPreferExistsRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(InPredicate node)
        {
            if (!node.NotDefined && node.Subquery is not null)
            {
                _issues.Add(_rule.CreateIssue(
                    _context,
                    node.GetSourceLocation(),
                    "IN (subquery) can usually be expressed as EXISTS for an existence / semi-join check.",
                    "Prefer EXISTS (SELECT 1 FROM … WHERE …) when checking related rows."));
            }

            base.ExplicitVisit(node);
        }
    }
}
