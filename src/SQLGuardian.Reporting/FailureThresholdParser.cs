using SQLGuardian.Abstractions;

namespace SQLGuardian.Reporting;

public static class FailureThresholdParser
{
    /// <summary>
    /// Parses <c>--fail-on</c> values. Returns null for never-fail.
    /// </summary>
    public static Severity? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Severity.High;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "never" or "none" or "off" => null,
            "critical" => Severity.Critical,
            "high" or "error" => Severity.High,
            "medium" or "warning" or "warn" => Severity.Medium,
            "low" => Severity.Low,
            "info" or "information" or "any" => Severity.Info,
            _ => throw new ArgumentException(
                $"Unknown --fail-on value '{value}'. Use critical, high, medium, low, info, or never.")
        };
    }
}
