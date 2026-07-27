using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0013 — Non-SARGable function / CAST / CONVERT on a column.</summary>
public sealed class NonSargableFunctionOnColumnRule : SqlRuleBase
{
    public override string RuleId => "SQLG0013";
    public override string Title => "Non-SARGable function on column";
    public override string Description =>
        "Applying functions or CAST/CONVERT to columns in predicates often prevents index seeks.";
    public override Severity Severity => Severity.Medium;
    public override RuleCategory Category => RuleCategory.Performance;
    public override IReadOnlyList<string> Tags { get; } = ["sargable", "function", "index", "where"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        fragment.Accept(new Visitor(this, context, issues));
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly NonSargableFunctionOnColumnRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly ICollection<Issue> _issues;
        private int _predicateDepth;

        public Visitor(NonSargableFunctionOnColumnRule rule, SqlAnalysisContext context, ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _issues = issues;
        }

        public override void ExplicitVisit(WhereClause node)
        {
            _predicateDepth++;
            base.ExplicitVisit(node);
            _predicateDepth--;
        }

        public override void ExplicitVisit(HavingClause node)
        {
            _predicateDepth++;
            base.ExplicitVisit(node);
            _predicateDepth--;
        }

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            if (_predicateDepth > 0)
            {
                Consider(node.FirstExpression, node);
                Consider(node.SecondExpression, node);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(LikePredicate node)
        {
            if (_predicateDepth > 0)
            {
                Consider(node.FirstExpression, node);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            if (node.SearchCondition is not null)
            {
                _predicateDepth++;
                // Visit only the ON predicate tree once (avoid double-walk via base).
                node.SearchCondition.Accept(this);
                _predicateDepth--;
            }

            node.FirstTableReference?.Accept(this);
            node.SecondTableReference?.Accept(this);
        }

        private void Consider(ScalarExpression? expression, TSqlFragment locationNode)
        {
            if (expression is null || !WrapsColumn(expression))
            {
                return;
            }

            var label = Describe(expression);
            _issues.Add(_rule.CreateIssue(
                _context,
                locationNode.GetSourceLocation(),
                $"Predicate applies {label} to a column, which is typically non-SARGable.",
                "Rewrite so the column stands alone (e.g. range predicates instead of YEAR(col))."));
        }

        private static bool WrapsColumn(ScalarExpression expression) =>
            expression switch
            {
                FunctionCall call => ArgumentsContainColumn(call.Parameters),
                ConvertCall convert => ContainsColumn(convert.Parameter),
                CastCall cast => ContainsColumn(cast.Parameter),
                UnaryExpression unary => WrapsColumn(unary.Expression),
                ParenthesisExpression paren => WrapsColumn(paren.Expression),
                _ => false
            };

        private static bool ArgumentsContainColumn(IList<ScalarExpression>? parameters)
        {
            if (parameters is null)
            {
                return false;
            }

            foreach (var p in parameters)
            {
                if (ContainsColumn(p))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsColumn(ScalarExpression? expression)
        {
            if (expression is null)
            {
                return false;
            }

            var finder = new ColumnFinder();
            expression.Accept(finder);
            return finder.Found;
        }

        private static string Describe(ScalarExpression expression) =>
            expression switch
            {
                FunctionCall call => $"function '{ScriptDomNaming.IdentifierValue(call.FunctionName)}'",
                ConvertCall => "CONVERT",
                CastCall => "CAST",
                _ => "a function/cast"
            };

        private sealed class ColumnFinder : TSqlConcreteFragmentVisitor
        {
            public bool Found { get; private set; }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                Found = true;
            }
        }
    }
}
