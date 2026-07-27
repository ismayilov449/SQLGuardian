using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom.Visitors;

public enum SqlJoinKind
{
    Inner,
    LeftOuter,
    RightOuter,
    FullOuter,
    CrossJoin,
    CrossApply,
    OuterApply
}

/// <summary>
/// A join between table references. Rule-agnostic.
/// </summary>
public sealed record JoinOccurrence(
    SqlJoinKind Kind,
    bool HasSearchCondition,
    SourceLocation Location);
