namespace SQLGuardian.Abstractions;

/// <summary>
/// Result of parsing a SQL script.
/// </summary>
public sealed class SqlParseResult
{
    public required string SourceText { get; init; }

    public required string FilePath { get; init; }

    /// <summary>Parsed syntax tree root, or null if parsing produced no usable tree.</summary>
    public object? SyntaxTree { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool Success => Errors.Count == 0 && SyntaxTree is not null;

    public SqlAnalysisContext ToAnalysisContext(Schema.SchemaSnapshot? schema = null) => new()
    {
        SourceText = SourceText,
        FilePath = FilePath,
        SyntaxTree = SyntaxTree,
        ParseErrors = Errors,
        Schema = schema
    };
}
