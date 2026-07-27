using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0015 — CREATE/ALTER PROCEDURE or TRIGGER without SET NOCOUNT ON.</summary>
public sealed class MissingSetNocountOnRule : SqlRuleBase
{
    public override string RuleId => "SQLG0015";
    public override string Title => "Missing SET NOCOUNT ON in module";
    public override string Description =>
        "Procedures and triggers should set NOCOUNT ON to avoid extra DONE_IN_PROC messages.";
    public override Severity Severity => Severity.Low;
    public override RuleCategory Category => RuleCategory.BestPractices;
    public override IReadOnlyList<string> Tags { get; } = ["nocount", "procedure", "trigger"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        fragment.Accept(new Visitor(this, context, issues));
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly MissingSetNocountOnRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(MissingSetNocountOnRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(CreateProcedureStatement node) =>
            ConsiderModule(node, node.StatementList, "PROCEDURE");

        public override void ExplicitVisit(AlterProcedureStatement node) =>
            ConsiderModule(node, node.StatementList, "PROCEDURE");

        public override void ExplicitVisit(CreateTriggerStatement node) =>
            ConsiderModule(node, node.StatementList, "TRIGGER");

        public override void ExplicitVisit(AlterTriggerStatement node) =>
            ConsiderModule(node, node.StatementList, "TRIGGER");

        private void ConsiderModule(TSqlFragment node, StatementList? body, string kind)
        {
            if (body is null || HasSetNocountOn(body))
            {
                return;
            }

            _issues.Add(_rule.CreateIssue(
                _context,
                node.GetSourceLocation(),
                $"{kind} body does not contain SET NOCOUNT ON.",
                "Add SET NOCOUNT ON; near the start of the module body.",
                "SET NOCOUNT ON;"));
        }

        private static bool HasSetNocountOn(StatementList body)
        {
            var finder = new NocountFinder();
            body.Accept(finder);
            return finder.Found;
        }

        private sealed class NocountFinder : TSqlConcreteFragmentVisitor
        {
            public bool Found { get; private set; }

            public override void ExplicitVisit(PredicateSetStatement node)
            {
                // SET NOCOUNT ON is a PredicateSetStatement with Options.NoCount in modern ScriptDom.
                if (node.Options == SetOptions.NoCount && node.IsOn)
                {
                    Found = true;
                }

                base.ExplicitVisit(node);
            }
        }
    }
}
