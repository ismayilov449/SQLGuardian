using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// Collects scalar and table-valued function calls. Knows nothing about rules.
/// </summary>
public sealed class FunctionVisitor : TSqlConcreteFragmentVisitor
{
    private readonly List<FunctionOccurrence> _functions = [];

    public IReadOnlyList<FunctionOccurrence> Functions => _functions;

    public override void ExplicitVisit(FunctionCall node)
    {
        _functions.Add(new FunctionOccurrence(
            Name: ScriptDomNaming.IdentifierValue(node.FunctionName) ?? string.Empty,
            Qualifier: FormatCallTarget(node.CallTarget),
            ArgumentCount: node.Parameters?.Count ?? 0,
            IsTableValued: false,
            Location: node.GetSourceLocation()));

        base.ExplicitVisit(node);
    }

    public override void ExplicitVisit(SchemaObjectFunctionTableReference node)
    {
        _functions.Add(new FunctionOccurrence(
            Name: ScriptDomNaming.IdentifierValue(node.SchemaObject?.BaseIdentifier) ?? string.Empty,
            Qualifier: ScriptDomNaming.FormatParts(
                ScriptDomNaming.IdentifierValue(node.SchemaObject?.ServerIdentifier),
                ScriptDomNaming.IdentifierValue(node.SchemaObject?.DatabaseIdentifier),
                ScriptDomNaming.IdentifierValue(node.SchemaObject?.SchemaIdentifier)),
            ArgumentCount: node.Parameters?.Count ?? 0,
            IsTableValued: true,
            Location: node.GetSourceLocation()));

        base.ExplicitVisit(node);
    }

    private static string? FormatCallTarget(CallTarget? target) => target switch
    {
        MultiPartIdentifierCallTarget multi => ScriptDomNaming.Format(multi.MultiPartIdentifier),
        _ => null
    };

    public static FunctionVisitor Collect(TSqlFragment root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var visitor = new FunctionVisitor();
        root.Accept(visitor);
        return visitor;
    }
}
