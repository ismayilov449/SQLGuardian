namespace SQLGuardian.Reporting;

public sealed class ReportWriteOptions
{
    /// <summary>
    /// Optional directory used to emit relative artifact URIs (especially for SARIF in CI).
    /// </summary>
    public string? BaseDirectory { get; init; }
}
