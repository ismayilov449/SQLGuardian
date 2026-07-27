using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0003 — Avoid NOLOCK / READUNCOMMITTED hints.</summary>
public sealed class NoLockRule : SqlRuleBase
{
    public override string RuleId => "SQLG0003";
    public override string Title => "Avoid NOLOCK table hint";
    public override string Description =>
        "NOLOCK allows dirty reads and can return incorrect results under concurrency.";
    public override Severity Severity => Severity.High;
    public override RuleCategory Category => RuleCategory.Concurrency;
    public override IReadOnlyList<string> Tags { get; } = ["nolock", "isolation", "dirty-read"];

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
        private readonly NoLockRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(NoLockRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(NamedTableReference node)
        {
            foreach (var hint in node.TableHints ?? [])
            {
                if (hint.HintKind is TableHintKind.NoLock or TableHintKind.ReadUncommitted)
                {
                    _issues.Add(_rule.CreateIssue(
                        _context,
                        hint.GetSourceLocation(),
                        $"Table hint {hint.HintKind} can return dirty or inconsistent reads.",
                        "Remove the hint or choose an intentional isolation strategy."));
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
