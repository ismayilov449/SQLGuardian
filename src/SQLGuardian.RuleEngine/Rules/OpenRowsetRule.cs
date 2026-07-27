using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0020 — OPENROWSET / OPENDATASOURCE.</summary>
public sealed class OpenRowsetRule : SqlRuleBase
{
    public override string RuleId => "SQLG0020";
    public override string Title => "Avoid OPENROWSET / OPENDATASOURCE";
    public override string Description =>
        "Ad-hoc distributed queries expand the attack surface versus controlled linked servers.";
    public override Severity Severity => Severity.High;
    public override RuleCategory Category => RuleCategory.Security;
    public override IReadOnlyList<string> Tags { get; } = ["openrowset", "opendatasource", "security"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        fragment.Accept(new Visitor(this, context, issues));
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly OpenRowsetRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(OpenRowsetRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(OpenRowsetTableReference node)
        {
            Flag(node, "OPENROWSET");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(OpenQueryTableReference node)
        {
            Flag(node, "OPENQUERY");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SchemaObjectFunctionTableReference node)
        {
            var name = ScriptDomNaming.IdentifierValue(node.SchemaObject?.BaseIdentifier);
            if (name is not null
                && (name.Equals("OPENDATASOURCE", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("OPENROWSET", StringComparison.OrdinalIgnoreCase)))
            {
                Flag(node, name.ToUpperInvariant());
            }

            base.ExplicitVisit(node);
        }

        private void Flag(TSqlFragment node, string name)
        {
            _issues.Add(_rule.CreateIssue(
                _context,
                node.GetSourceLocation(),
                $"{name} enables ad-hoc remote access and is restricted in hardened environments.",
                "Prefer linked servers with least privilege, or move extraction outside T-SQL."));
        }
    }
}
