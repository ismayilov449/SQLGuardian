namespace SQLGuardian.Abstractions;

/// <summary>
/// Issue severity. Users may override per-rule severity via configuration.
/// </summary>
public enum Severity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
