using SQLGuardian.Abstractions;

namespace SQLGuardian.Domain;

/// <summary>
/// Multi-file analysis aggregate used by reporters and the CLI.
/// </summary>
public sealed class AnalysisRun
{
    public required IReadOnlyList<AnalysisReport> Reports { get; init; }

    public string ToolName { get; init; } = "SQLGuardian";

    public string ToolVersion { get; init; } = "0.4.0";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public int FileCount => Reports.Count;

    public int IssueCount => Reports.Sum(r => r.IssueCount);

    public int ParseErrorCount => Reports.Sum(r => r.ParseErrors.Count);

    public IEnumerable<Issue> AllIssues => Reports.SelectMany(r => r.Issues);

    public bool MeetsFailureThreshold(Severity minimumSeverity) =>
        ParseErrorCount > 0
        || AllIssues.Any(i => i.Severity >= minimumSeverity);
}
