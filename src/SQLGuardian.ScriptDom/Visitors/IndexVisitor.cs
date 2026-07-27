using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// Collects index DDL statements. Knows nothing about rules.
/// </summary>
public sealed class IndexVisitor : TSqlConcreteFragmentVisitor
{
    private readonly List<IndexOccurrence> _indexes = [];

    public IReadOnlyList<IndexOccurrence> Indexes => _indexes;

    public override void ExplicitVisit(CreateIndexStatement node)
    {
        _indexes.Add(new IndexOccurrence(
            Operation: IndexOperationKind.Create,
            IndexName: ScriptDomNaming.IdentifierValue(node.Name),
            TableName: ScriptDomNaming.Format(node.OnName),
            IsUnique: node.Unique,
            IsClustered: node.Clustered,
            Location: node.GetSourceLocation()));

        base.ExplicitVisit(node);
    }

    public override void ExplicitVisit(AlterIndexStatement node)
    {
        _indexes.Add(new IndexOccurrence(
            Operation: IndexOperationKind.Alter,
            IndexName: ScriptDomNaming.IdentifierValue(node.Name),
            TableName: ScriptDomNaming.Format(node.OnName),
            IsUnique: null,
            IsClustered: null,
            Location: node.GetSourceLocation()));

        base.ExplicitVisit(node);
    }

    public override void ExplicitVisit(DropIndexStatement node)
    {
        foreach (var clause in node.DropIndexClauses.OfType<DropIndexClause>())
        {
            _indexes.Add(new IndexOccurrence(
                Operation: IndexOperationKind.Drop,
                IndexName: ScriptDomNaming.IdentifierValue(clause.Index),
                TableName: ScriptDomNaming.Format(clause.Object),
                IsUnique: null,
                IsClustered: null,
                Location: clause.GetSourceLocation()));
        }

        base.ExplicitVisit(node);
    }

    public static IndexVisitor Collect(TSqlFragment root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var visitor = new IndexVisitor();
        root.Accept(visitor);
        return visitor;
    }
}
