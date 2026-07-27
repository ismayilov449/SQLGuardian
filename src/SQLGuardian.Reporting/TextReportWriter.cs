using System.Text;
using SQLGuardian.Abstractions;
using SQLGuardian.Domain;

namespace SQLGuardian.Reporting;

public sealed class TextReportWriter : IReportWriter
{
    public ReportFormat Format => ReportFormat.Text;

    public string Write(AnalysisRun run, ReportWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        var sb = new StringBuilder();

        foreach (var report in run.Reports)
        {
            sb.AppendLine($"{report.Target}: {report.IssueCount} issue(s), {report.ParseErrors.Count} parse error(s)");

            foreach (var error in report.ParseErrors)
            {
                sb.AppendLine($"  parse: {error}");
            }

            foreach (var issue in report.Issues.OrderBy(i => i.Location.StartLine).ThenBy(i => i.Location.StartColumn))
            {
                sb.AppendLine(
                    $"  {issue.Severity} {issue.RuleId} L{issue.Location.StartLine}:{issue.Location.StartColumn} {issue.Message}");
                if (!string.IsNullOrWhiteSpace(issue.Suggestion))
                {
                    sb.AppendLine($"    suggestion: {issue.Suggestion}");
                }

                if (!string.IsNullOrWhiteSpace(issue.SuggestedSql))
                {
                    foreach (var line in issue.SuggestedSql.Split('\n'))
                    {
                        sb.AppendLine($"    sql> {line.TrimEnd('\r')}");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(report.RecommendedSql))
            {
                sb.AppendLine("  --- recommended SQL ---");
                foreach (var line in report.RecommendedSql.Split('\n'))
                {
                    sb.AppendLine($"  {line.TrimEnd('\r')}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine(
            $"Analyzed {run.FileCount} file(s). Issues: {run.IssueCount}. Parse errors: {run.ParseErrorCount}.");

        return sb.ToString();
    }
}
