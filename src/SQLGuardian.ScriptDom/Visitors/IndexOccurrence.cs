using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom.Visitors;

public enum IndexOperationKind
{
    Create,
    Alter,
    Drop
}

/// <summary>
/// An index DDL statement. Rule-agnostic.
/// </summary>
public sealed record IndexOccurrence(
    IndexOperationKind Operation,
    string? IndexName,
    string? TableName,
    bool? IsUnique,
    bool? IsClustered,
    SourceLocation Location);
