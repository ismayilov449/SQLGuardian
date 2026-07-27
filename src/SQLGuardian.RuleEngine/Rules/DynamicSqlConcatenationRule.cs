using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0014 — Dynamic SQL built with string concatenation.</summary>
public sealed class DynamicSqlConcatenationRule : SqlRuleBase
{
    public override string RuleId => "SQLG0014";
    public override string Title => "Dynamic SQL string concatenation";
    public override string Description =>
        "Executing SQL built with string concatenation risks injection; prefer parameterized sp_executesql.";
    public override Severity Severity => Severity.High;
    public override RuleCategory Category => RuleCategory.Security;
    public override IReadOnlyList<string> Tags { get; } = ["dynamic-sql", "injection", "execute"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        fragment.Accept(new Visitor(this, context, issues));
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly DynamicSqlConcatenationRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;

        public Visitor(DynamicSqlConcatenationRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(ExecuteStatement node)
        {
            ConsiderExecute(node.ExecuteSpecification);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ExecuteSpecification node)
        {
            ConsiderExecute(node);
            base.ExplicitVisit(node);
        }

        private void ConsiderExecute(ExecuteSpecification? spec)
        {
            if (spec?.ExecutableEntity is not ExecutableStringList list)
            {
                return;
            }

            // ScriptDom often splits concatenations into multiple Strings entries.
            if (list.Strings.Count > 1)
            {
                Flag(spec);
                return;
            }

            foreach (var item in list.Strings)
            {
                if (ContainsConcatenation(item) || item is VariableReference)
                {
                    // EXEC(@sql) — variable target; still dynamic, but concatenation may be earlier.
                    // Only flag variable-only when it's clearly EXEC(@x); keep concat-focused rule precise.
                    if (item is VariableReference)
                    {
                        continue;
                    }

                    Flag(spec);
                    return;
                }
            }
        }

        private void Flag(TSqlFragment node)
        {
            _issues.Add(_rule.CreateIssue(
                _context,
                node.GetSourceLocation(),
                "Dynamic SQL is built with string concatenation before EXECUTE.",
                "Use sys.sp_executesql with parameters instead of concatenating values into SQL text."));
        }

        private static bool ContainsConcatenation(ScalarExpression? expression)
        {
            if (expression is null)
            {
                return false;
            }

            var finder = new ConcatFinder();
            expression.Accept(finder);
            return finder.Found;
        }

        private sealed class ConcatFinder : TSqlConcreteFragmentVisitor
        {
            public bool Found { get; private set; }

            public override void ExplicitVisit(BinaryExpression node)
            {
                if (node.BinaryExpressionType == BinaryExpressionType.Add)
                {
                    Found = true;
                }

                base.ExplicitVisit(node);
            }
        }
    }
}
