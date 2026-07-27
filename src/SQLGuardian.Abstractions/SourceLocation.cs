namespace SQLGuardian.Abstractions;

/// <summary>
/// 1-based source location within a SQL script.
/// </summary>
public sealed record SourceLocation(
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn)
{
    public static SourceLocation Unknown { get; } = new(1, 1, 1, 1);
}
