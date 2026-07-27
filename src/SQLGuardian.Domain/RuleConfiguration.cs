using SQLGuardian.Abstractions;

namespace SQLGuardian.Domain;

/// <summary>
/// Per-rule severity overrides and enable/disable flags (EditorConfig-style).
/// </summary>
public sealed class RuleConfiguration
{
    private readonly Dictionary<string, SeverityOverride> _overrides =
        new(StringComparer.OrdinalIgnoreCase);

    public void Set(string ruleId, Severity severity) =>
        _overrides[ruleId] = new SeverityOverride(Enabled: true, Severity: severity);

    public void Disable(string ruleId) =>
        _overrides[ruleId] = new SeverityOverride(Enabled: false, Severity: null);

    public bool IsEnabled(string ruleId) =>
        !_overrides.TryGetValue(ruleId, out var value) || value.Enabled;

    public Severity ResolveSeverity(string ruleId, Severity defaultSeverity) =>
        _overrides.TryGetValue(ruleId, out var value) && value is { Enabled: true, Severity: { } severity }
            ? severity
            : defaultSeverity;

    private sealed record SeverityOverride(bool Enabled, Severity? Severity);
}
