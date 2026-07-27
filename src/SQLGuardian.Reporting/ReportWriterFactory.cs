namespace SQLGuardian.Reporting;

public static class ReportWriterFactory
{
    public static IReportWriter Create(ReportFormat format) => format switch
    {
        ReportFormat.Text => new TextReportWriter(),
        ReportFormat.Json => new JsonReportWriter(),
        ReportFormat.Sarif => new SarifReportWriter(),
        ReportFormat.Markdown => new MarkdownReportWriter(),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported report format.")
    };

    public static bool TryParseFormat(string? value, out ReportFormat format)
    {
        format = ReportFormat.Text;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "text":
            case "console":
            case "plain":
                format = ReportFormat.Text;
                return true;
            case "json":
                format = ReportFormat.Json;
                return true;
            case "sarif":
            case "sarif-2.1.0":
                format = ReportFormat.Sarif;
                return true;
            case "markdown":
            case "md":
                format = ReportFormat.Markdown;
                return true;
            default:
                return false;
        }
    }
}
