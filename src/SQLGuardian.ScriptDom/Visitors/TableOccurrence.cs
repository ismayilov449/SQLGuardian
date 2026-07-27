using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// A named table reference discovered in the AST. Rule-agnostic.
/// </summary>
public sealed record TableOccurrence(
    string? Server,
    string? Database,
    string? Schema,
    string Name,
    string? Alias,
    bool HasTableHints,
    SourceLocation Location);
