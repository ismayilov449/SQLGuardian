using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom;

/// <summary>
/// Maps ScriptDom fragments to <see cref="SourceLocation"/>.
/// </summary>
public static class FragmentExtensions
{
    public static SourceLocation GetSourceLocation(this TSqlFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        if (fragment.StartLine <= 0)
        {
            return SourceLocation.Unknown;
        }

        var endLine = fragment.StartLine;
        var endColumn = fragment.StartColumn;

        if (fragment.ScriptTokenStream is { Count: > 0 } tokens
            && fragment.LastTokenIndex >= 0
            && fragment.LastTokenIndex < tokens.Count)
        {
            var last = tokens[fragment.LastTokenIndex];
            endLine = last.Line > 0 ? last.Line : endLine;
            endColumn = last.Column + (last.Text?.Length ?? 0);
            if (endColumn < 1)
            {
                endColumn = 1;
            }
        }

        return new SourceLocation(
            fragment.StartLine,
            Math.Max(1, fragment.StartColumn),
            endLine,
            Math.Max(1, endColumn));
    }
}
