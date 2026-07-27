using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0002 — UPDATE/DELETE without WHERE.</summary>
public sealed class MissingWhereRule : SqlRuleBase
{
    public override string RuleId => "SQLG0002";
    public override string Title => "UPDATE/DELETE without WHERE";
    public override string Description =>
        "UPDATE or DELETE without WHERE can modify or remove every row.";
    public override Severity Severity => Severity.Critical;
    public override RuleCategory Category => RuleCategory.Correctness;
    public override IReadOnlyList<string> Tags { get; } = ["update", "delete", "where", "data-loss"];

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
        private readonly MissingWhereRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(MissingWhereRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(UpdateStatement node)
        {
            if (node.UpdateSpecification?.WhereClause is null)
            {
                _issues.Add(_rule.CreateIssue(
                    _context,
                    node.GetSourceLocation(),
                    "UPDATE without a WHERE clause affects all rows in the target.",
                    "Add a WHERE clause that limits the rows to update.",
                    "UPDATE /* target */\nSET /* columns */\nWHERE /* key = @id */;  -- required filter"));
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            if (node.DeleteSpecification?.WhereClause is null)
            {
                _issues.Add(_rule.CreateIssue(
                    _context,
                    node.GetSourceLocation(),
                    "DELETE without a WHERE clause removes all rows from the target.",
                    "Add a WHERE clause that limits the rows to delete.",
                    "DELETE FROM /* target */\nWHERE /* key = @id */;  -- required filter"));
            }

            base.ExplicitVisit(node);
        }
    }
}
