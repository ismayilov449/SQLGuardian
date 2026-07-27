using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// A scalar or table-valued function invocation. Rule-agnostic.
/// </summary>
public sealed record FunctionOccurrence(
    string Name,
    string? Qualifier,
    int ArgumentCount,
    bool IsTableValued,
    SourceLocation Location);
