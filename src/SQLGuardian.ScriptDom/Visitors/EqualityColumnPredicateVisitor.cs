using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// Collects equality comparisons between two column references (typical join predicates).
/// </summary>
public sealed class EqualityColumnPredicateVisitor : TSqlConcreteFragmentVisitor
{
    private readonly List<EqualityColumnPredicate> _predicates = [];

    public IReadOnlyList<EqualityColumnPredicate> Predicates => _predicates;

    public override void ExplicitVisit(BooleanComparisonExpression node)
    {
        if (node.ComparisonType == BooleanComparisonType.Equals
            && node.FirstExpression is ColumnReferenceExpression left
            && node.SecondExpression is ColumnReferenceExpression right)
        {
            _predicates.Add(new EqualityColumnPredicate(
                Left: ToRef(left),
                Right: ToRef(right),
                Location: node.GetSourceLocation()));
        }

        base.ExplicitVisit(node);
    }

    private static ColumnRef ToRef(ColumnReferenceExpression column)
    {
        var identifiers = column.MultiPartIdentifier?.Identifiers;
        if (identifiers is null || identifiers.Count == 0)
        {
            return new ColumnRef(null, string.Empty);
        }

        var columnName = ScriptDomNaming.IdentifierValue(identifiers[^1]) ?? string.Empty;
        string? qualifier = null;
        if (identifiers.Count >= 2)
        {
            qualifier = ScriptDomNaming.IdentifierValue(identifiers[^2]);
        }

        return new ColumnRef(qualifier, columnName);
    }

    public static EqualityColumnPredicateVisitor Collect(TSqlFragment root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var visitor = new EqualityColumnPredicateVisitor();
        root.Accept(visitor);
        return visitor;
    }
}

public sealed record ColumnRef(string? Qualifier, string ColumnName);

public sealed record EqualityColumnPredicate(
    ColumnRef Left,
    ColumnRef Right,
    SourceLocation Location);
