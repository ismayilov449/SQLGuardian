using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom;

/// <summary>
/// Parses T-SQL using Microsoft.SqlServer.TransactSql.ScriptDom.
/// This is the only allowed parse path — never use regex for SQL analysis.
/// Pair with <c>SQLGuardian.ScriptDom.Visitors</c> for AST walks.
/// </summary>
public sealed class ScriptDomSqlParser : ISqlParser
{
    public SqlParseResult Parse(string sourceText, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        var path = string.IsNullOrWhiteSpace(filePath) ? "<memory>" : filePath;
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);

        using var reader = new StringReader(sourceText);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        var errorMessages = errors
            .Select(e => $"Line {e.Line}, Col {e.Column}: {e.Message}")
            .ToList();

        return new SqlParseResult
        {
            SourceText = sourceText,
            FilePath = path,
            SyntaxTree = fragment,
            Errors = errorMessages
        };
    }
}
