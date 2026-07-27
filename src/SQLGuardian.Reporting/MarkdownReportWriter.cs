using System.Text;
using SQLGuardian.Abstractions;
using SQLGuardian.Domain;

namespace SQLGuardian.Reporting;

public sealed class MarkdownReportWriter : IReportWriter
{
    public ReportFormat Format => ReportFormat.Markdown;

    public string Write(AnalysisRun run, ReportWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        var sb = new StringBuilder();

        sb.AppendLine($"# {run.ToolName} Report");
        sb.AppendLine();
        sb.AppendLine($"- **Version:** {run.ToolVersion}");
        sb.AppendLine($"- **Generated:** {run.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- **Files:** {run.FileCount}");
        sb.AppendLine($"- **Issues:** {run.IssueCount}");
        sb.AppendLine($"- **Parse errors:** {run.ParseErrorCount}");
        sb.AppendLine();

        if (run.IssueCount == 0 && run.ParseErrorCount == 0)
        {
            sb.AppendLine("No issues found.");
            return sb.ToString();
        }

        foreach (var report in run.Reports)
        {
            var path = ReportPathHelper.NormalizePath(report.Target, options?.BaseDirectory);
            sb.AppendLine($"## `{path}`");
            sb.AppendLine();

            if (report.ParseErrors.Count > 0)
            {
                sb.AppendLine("### Parse errors");
                sb.AppendLine();
                foreach (var error in report.ParseErrors)
                {
                    sb.AppendLine($"- {error}");
                }

                sb.AppendLine();
            }

            if (report.Issues.Count == 0)
            {
                sb.AppendLine("_No rule issues._");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine("| Severity | Rule | Line | Message | Suggestion |");
            sb.AppendLine("|----------|------|------|---------|------------|");

            foreach (var issue in report.Issues
                         .OrderByDescending(i => i.Severity)
                         .ThenBy(i => i.Location.StartLine))
            {
                sb.Append("| ")
                    .Append(issue.Severity).Append(" | ")
                    .Append(issue.RuleId).Append(" | ")
                    .Append(issue.Location.StartLine).Append(':').Append(issue.Location.StartColumn).Append(" | ")
                    .Append(Escape(issue.Message)).Append(" | ")
                    .Append(Escape(issue.Suggestion ?? string.Empty)).AppendLine(" |");
            }

            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(report.RecommendedSql))
            {
                sb.AppendLine("### Recommended SQL");
                sb.AppendLine();
                sb.AppendLine("```sql");
                sb.AppendLine(report.RecommendedSql.TrimEnd());
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ');
}
