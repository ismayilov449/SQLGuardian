using SQLGuardian.Domain;

namespace SQLGuardian.Reporting;

public interface IReportWriter
{
    ReportFormat Format { get; }

    string Write(AnalysisRun run, ReportWriteOptions? options = null);
}
