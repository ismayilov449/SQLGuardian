using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// A column reference or SELECT * wildcard. Rule-agnostic.
/// </summary>
public sealed record ColumnOccurrence(
    string Qualifier,
    string? ColumnName,
    bool IsWildcard,
    SourceLocation Location);
