namespace SQLGuardian.Abstractions.Schema;

public sealed class IndexSchema
{
    public required string Name { get; init; }

    public bool IsUnique { get; init; }

    public bool IsPrimaryKey { get; init; }

    public bool IsUniqueConstraint { get; init; }

    /// <summary>Key columns in key ordinal order (leading column first).</summary>
    public IReadOnlyList<string> KeyColumns { get; init; } = [];

    public IReadOnlyList<string> IncludedColumns { get; init; } = [];
}
