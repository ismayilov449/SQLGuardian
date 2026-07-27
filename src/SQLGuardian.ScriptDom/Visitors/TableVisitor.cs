using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// Collects named table references. Knows nothing about rules.
/// </summary>
public sealed class TableVisitor : TSqlConcreteFragmentVisitor
{
    private readonly List<TableOccurrence> _tables = [];

    public IReadOnlyList<TableOccurrence> Tables => _tables;

    public override void ExplicitVisit(NamedTableReference node)
    {
        var schemaObject = node.SchemaObject;
        _tables.Add(new TableOccurrence(
            Server: ScriptDomNaming.IdentifierValue(schemaObject?.ServerIdentifier),
            Database: ScriptDomNaming.IdentifierValue(schemaObject?.DatabaseIdentifier),
            Schema: ScriptDomNaming.IdentifierValue(schemaObject?.SchemaIdentifier),
            Name: ScriptDomNaming.IdentifierValue(schemaObject?.BaseIdentifier) ?? string.Empty,
            Alias: ScriptDomNaming.IdentifierValue(node.Alias),
            HasTableHints: node.TableHints is { Count: > 0 },
            Location: node.GetSourceLocation()));

        base.ExplicitVisit(node);
    }

    public static TableVisitor Collect(TSqlFragment root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var visitor = new TableVisitor();
        root.Accept(visitor);
        return visitor;
    }
}
