using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom;

/// <summary>
/// Safe access to ScriptDom trees stored on <see cref="SqlAnalysisContext.SyntaxTree"/>.
/// </summary>
public static class ScriptDomSyntax
{
    public static TSqlFragment? AsFragment(object? syntaxTree) => syntaxTree as TSqlFragment;

    public static TSqlFragment? GetFragment(SqlAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return AsFragment(context.SyntaxTree);
    }

    public static bool TryGetFragment(SqlAnalysisContext context, out TSqlFragment fragment)
    {
        fragment = GetFragment(context)!;
        return fragment is not null;
    }
}
