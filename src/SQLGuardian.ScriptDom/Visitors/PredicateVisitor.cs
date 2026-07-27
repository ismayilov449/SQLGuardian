using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// Collects boolean predicates. Knows nothing about rules.
/// Tracks WHERE-clause nesting so consumers can filter search conditions.
/// Uses <see cref="TSqlConcreteFragmentVisitor"/> so children are visited
/// while the WHERE depth counter is active.
/// </summary>
public sealed class PredicateVisitor : TSqlConcreteFragmentVisitor
{
    private readonly List<PredicateOccurrence> _predicates = [];
    private int _whereDepth;

    public IReadOnlyList<PredicateOccurrence> Predicates => _predicates;

    public IEnumerable<PredicateOccurrence> WherePredicates =>
        _predicates.Where(p => p.IsInWhereClause);

    public override void ExplicitVisit(WhereClause node)
    {
        _whereDepth++;
        base.ExplicitVisit(node);
        _whereDepth--;
    }

    public override void ExplicitVisit(BooleanComparisonExpression node)
    {
        Add(PredicateKind.Comparison, isNegated: false, patternLiteral: null, node);
        base.ExplicitVisit(node);
    }

    public override void ExplicitVisit(LikePredicate node)
    {
        Add(
            PredicateKind.Like,
            isNegated: node.NotDefined,
            patternLiteral: TryGetStringLiteral(node.SecondExpression),
            node);
        base.ExplicitVisit(node);
    }

    public override void ExplicitVisit(InPredicate node)
    {
        Add(PredicateKind.In, isNegated: node.NotDefined, patternLiteral: null, node);
        base.ExplicitVisit(node);
    }

    public override void ExplicitVisit(BooleanIsNullExpression node)
    {
        Add(PredicateKind.IsNull, isNegated: node.IsNot, patternLiteral: null, node);
        base.ExplicitVisit(node);
    }

    public override void ExplicitVisit(ExistsPredicate node)
    {
        Add(PredicateKind.Exists, isNegated: false, patternLiteral: null, node);
        base.ExplicitVisit(node);
    }

    public override void ExplicitVisit(BooleanBinaryExpression node)
    {
        var kind = node.BinaryExpressionType == BooleanBinaryExpressionType.And
            ? PredicateKind.And
            : PredicateKind.Or;

        Add(kind, isNegated: false, patternLiteral: null, node);
        base.ExplicitVisit(node);
    }

    private void Add(PredicateKind kind, bool isNegated, string? patternLiteral, TSqlFragment node) =>
        _predicates.Add(new PredicateOccurrence(
            Kind: kind,
            IsNegated: isNegated,
            IsInWhereClause: _whereDepth > 0,
            PatternLiteral: patternLiteral,
            Location: node.GetSourceLocation()));

    private static string? TryGetStringLiteral(ScalarExpression? expression) =>
        expression is StringLiteral literal ? literal.Value : null;

    public static PredicateVisitor Collect(TSqlFragment root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var visitor = new PredicateVisitor();
        root.Accept(visitor);
        return visitor;
    }
}
