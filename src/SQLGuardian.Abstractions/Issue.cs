namespace SQLGuardian.Abstractions;

/// <summary>
/// A single finding produced by a rule.
/// </summary>
public sealed class Issue
{
    public required string RuleId { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public required Severity Severity { get; init; }

    public required RuleCategory Category { get; init; }

    public required SourceLocation Location { get; init; }

    /// <summary>Short human guidance.</summary>
    public string? Suggestion { get; init; }

    /// <summary>
    /// Ready-to-run SQL the user can copy (rewritten query, CREATE INDEX, etc.).
    /// Deterministic — never LLM-generated.
    /// </summary>
    public string? SuggestedSql { get; init; }

    public string? FilePath { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}
