using SQLGuardian.Abstractions;

namespace SQLGuardian.Domain;

/// <summary>
/// Aggregate result of analyzing one or more scripts with the rule engine.
/// </summary>
public sealed class AnalysisReport
{
    public required string Target { get; init; }

    public IReadOnlyList<Issue> Issues { get; init; } = [];

    public IReadOnlyList<string> ParseErrors { get; init; } = [];

    /// <summary>
    /// Combined copy-paste script of recommended fixes for this target (indexes + query rewrites).
    /// </summary>
    public string? RecommendedSql { get; init; }

    public int IssueCount => Issues.Count;

    public bool HasErrors =>
        ParseErrors.Count > 0
        || Issues.Any(i => i.Severity is Severity.High or Severity.Critical);
}
