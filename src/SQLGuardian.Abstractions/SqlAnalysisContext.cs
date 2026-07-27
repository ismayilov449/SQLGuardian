using SQLGuardian.Abstractions.Schema;

namespace SQLGuardian.Abstractions;

/// <summary>
/// Input to rule analysis. The syntax tree is provided by the ScriptDom layer
/// as an opaque handle so rules depend on visitors/helpers, not raw text.
/// </summary>
public sealed class SqlAnalysisContext
{
    /// <summary>Original SQL source. For diagnostics and messaging only — never for detection via regex.</summary>
    public required string SourceText { get; init; }

    /// <summary>Path or logical name of the script being analyzed.</summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Parsed syntax tree root (typically a ScriptDom <c>TSqlFragment</c>).
    /// Null when parsing failed; rules should no-op or rely on <see cref="ParseErrors"/>.
    /// </summary>
    public object? SyntaxTree { get; init; }

    public IReadOnlyList<string> ParseErrors { get; init; } = [];

    public bool HasParseErrors => ParseErrors.Count > 0;

    /// <summary>
    /// Optional catalog snapshot (schema + approximate row counts only).
    /// When null, schema-aware rules no-op.
    /// </summary>
    public SchemaSnapshot? Schema { get; init; }
}
