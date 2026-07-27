namespace SQLGuardian.Abstractions;

/// <summary>
/// Parses SQL into an AST. Implementations must use a real SQL parser (ScriptDom),
/// never regular expressions.
/// </summary>
public interface ISqlParser
{
    SqlParseResult Parse(string sourceText, string? filePath = null);
}
