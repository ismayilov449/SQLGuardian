using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// Collects column references and SELECT * wildcards. Knows nothing about rules.
/// </summary>
public sealed class ColumnVisitor : TSqlConcreteFragmentVisitor
{
    private readonly List<ColumnOccurrence> _columns = [];

    public IReadOnlyList<ColumnOccurrence> Columns => _columns;

    public override void ExplicitVisit(SelectStarExpression node)
    {
        _columns.Add(new ColumnOccurrence(
            Qualifier: ScriptDomNaming.Format(node.Qualifier),
            ColumnName: null,
            IsWildcard: true,
            Location: node.GetSourceLocation()));

        base.ExplicitVisit(node);
    }

    public override void ExplicitVisit(ColumnReferenceExpression node)
    {
        var parts = node.MultiPartIdentifier;
        string qualifier;
        string? columnName;

        if (parts is null || parts.Count == 0)
        {
            qualifier = string.Empty;
            columnName = null;
        }
        else if (parts.Count == 1)
        {
            qualifier = string.Empty;
            columnName = ScriptDomNaming.IdentifierValue(parts.Identifiers[0]);
        }
        else
        {
            var identifiers = parts.Identifiers;
            columnName = ScriptDomNaming.IdentifierValue(identifiers[^1]);
            qualifier = ScriptDomNaming.FormatParts(
                identifiers.Take(identifiers.Count - 1)
                    .Select(ScriptDomNaming.IdentifierValue)
                    .ToArray());
        }

        var isWildcard = node.ColumnType == ColumnType.Wildcard;

        _columns.Add(new ColumnOccurrence(
            Qualifier: qualifier,
            ColumnName: isWildcard ? null : columnName,
            IsWildcard: isWildcard,
            Location: node.GetSourceLocation()));

        base.ExplicitVisit(node);
    }

    public static ColumnVisitor Collect(TSqlFragment root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var visitor = new ColumnVisitor();
        root.Accept(visitor);
        return visitor;
    }
}
