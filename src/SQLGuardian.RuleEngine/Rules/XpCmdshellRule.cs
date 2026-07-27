using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0019 — xp_cmdshell usage.</summary>
public sealed class XpCmdshellRule : SqlRuleBase
{
    public override string RuleId => "SQLG0019";
    public override string Title => "Avoid xp_cmdshell";
    public override string Description =>
        "xp_cmdshell executes OS commands under the SQL Server service account.";
    public override Severity Severity => Severity.Critical;
    public override RuleCategory Category => RuleCategory.Security;
    public override IReadOnlyList<string> Tags { get; } = ["xp_cmdshell", "security", "surface"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        fragment.Accept(new Visitor(this, context, issues));
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly XpCmdshellRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(XpCmdshellRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(ExecutableProcedureReference node)
        {
            var name = GetProcedureName(node);
            if (name is not null
                && name.Equals("xp_cmdshell", StringComparison.OrdinalIgnoreCase))
            {
                _issues.Add(_rule.CreateIssue(
                    _context,
                    node.GetSourceLocation(),
                    "xp_cmdshell is a high-risk OS command surface.",
                    "Disable xp_cmdshell and use controlled alternatives (Agent jobs, external apps)."));
            }

            base.ExplicitVisit(node);
        }

        private static string? GetProcedureName(ExecutableProcedureReference node)
        {
            var proc = node.ProcedureReference?.ProcedureReference?.Name;
            if (proc is null)
            {
                return null;
            }

            return ScriptDomNaming.IdentifierValue(proc.BaseIdentifier);
        }
    }
}
