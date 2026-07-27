using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom.Visitors;

public enum PredicateKind
{
    Comparison,
    Like,
    In,
    IsNull,
    Exists,
    And,
    Or,
    Other
}

/// <summary>
/// A boolean predicate node. Rule-agnostic.
/// </summary>
public sealed record PredicateOccurrence(
    PredicateKind Kind,
    bool IsNegated,
    bool IsInWhereClause,
    string? PatternLiteral,
    SourceLocation Location);
