using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// Collects join nodes. Knows nothing about rules.
/// </summary>
public sealed class JoinVisitor : TSqlConcreteFragmentVisitor
{
    private readonly List<JoinOccurrence> _joins = [];

    public IReadOnlyList<JoinOccurrence> Joins => _joins;

    public override void ExplicitVisit(QualifiedJoin node)
    {
        _joins.Add(new JoinOccurrence(
            Kind: Map(node.QualifiedJoinType),
            HasSearchCondition: node.SearchCondition is not null,
            Location: node.GetSourceLocation()));

        base.ExplicitVisit(node);
    }

    public override void ExplicitVisit(UnqualifiedJoin node)
    {
        _joins.Add(new JoinOccurrence(
            Kind: Map(node.UnqualifiedJoinType),
            HasSearchCondition: false,
            Location: node.GetSourceLocation()));

        base.ExplicitVisit(node);
    }

    private static SqlJoinKind Map(QualifiedJoinType type) => type switch
    {
        QualifiedJoinType.Inner => SqlJoinKind.Inner,
        QualifiedJoinType.LeftOuter => SqlJoinKind.LeftOuter,
        QualifiedJoinType.RightOuter => SqlJoinKind.RightOuter,
        QualifiedJoinType.FullOuter => SqlJoinKind.FullOuter,
        _ => SqlJoinKind.Inner
    };

    private static SqlJoinKind Map(UnqualifiedJoinType type) => type switch
    {
        UnqualifiedJoinType.CrossJoin => SqlJoinKind.CrossJoin,
        UnqualifiedJoinType.CrossApply => SqlJoinKind.CrossApply,
        UnqualifiedJoinType.OuterApply => SqlJoinKind.OuterApply,
        _ => SqlJoinKind.CrossJoin
    };

    public static JoinVisitor Collect(TSqlFragment root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var visitor = new JoinVisitor();
        root.Accept(visitor);
        return visitor;
    }
}
