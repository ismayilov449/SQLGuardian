using System.Text.Json;
using System.Text.Json.Serialization;
using SQLGuardian.Abstractions;
using SQLGuardian.Domain;

namespace SQLGuardian.Reporting;

public sealed class JsonReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public ReportFormat Format => ReportFormat.Json;

    public string Write(AnalysisRun run, ReportWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(run);

        var payload = new JsonReportDocument
        {
            Tool = run.ToolName,
            Version = run.ToolVersion,
            GeneratedAt = run.GeneratedAt,
            FileCount = run.FileCount,
            IssueCount = run.IssueCount,
            ParseErrorCount = run.ParseErrorCount,
            Files = run.Reports.Select(r => new JsonFileReport
            {
                Path = ReportPathHelper.NormalizePath(r.Target, options?.BaseDirectory),
                ParseErrors = r.ParseErrors.ToList(),
                RecommendedSql = r.RecommendedSql,
                Issues = r.Issues.Select(MapIssue).ToList()
            }).ToList()
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static JsonIssue MapIssue(Issue issue) => new()
    {
        RuleId = issue.RuleId,
        Title = issue.Title,
        Message = issue.Message,
        Severity = issue.Severity,
        Category = issue.Category,
        Suggestion = issue.Suggestion,
        SuggestedSql = issue.SuggestedSql,
        FilePath = issue.FilePath,
        Tags = issue.Tags.ToList(),
        Location = new JsonLocation
        {
            StartLine = issue.Location.StartLine,
            StartColumn = issue.Location.StartColumn,
            EndLine = issue.Location.EndLine,
            EndColumn = issue.Location.EndColumn
        }
    };

    private sealed class JsonReportDocument
    {
        public required string Tool { get; init; }
        public required string Version { get; init; }
        public DateTimeOffset GeneratedAt { get; init; }
        public int FileCount { get; init; }
        public int IssueCount { get; init; }
        public int ParseErrorCount { get; init; }
        public required List<JsonFileReport> Files { get; init; }
    }

    private sealed class JsonFileReport
    {
        public required string Path { get; init; }
        public required List<string> ParseErrors { get; init; }
        public string? RecommendedSql { get; init; }
        public required List<JsonIssue> Issues { get; init; }
    }

    private sealed class JsonIssue
    {
        public required string RuleId { get; init; }
        public required string Title { get; init; }
        public required string Message { get; init; }
        public required Severity Severity { get; init; }
        public required RuleCategory Category { get; init; }
        public string? Suggestion { get; init; }
        public string? SuggestedSql { get; init; }
        public string? FilePath { get; init; }
        public required List<string> Tags { get; init; }
        public required JsonLocation Location { get; init; }
    }

    private sealed class JsonLocation
    {
        public int StartLine { get; init; }
        public int StartColumn { get; init; }
        public int EndLine { get; init; }
        public int EndColumn { get; init; }
    }
}
