using SQLGuardian.Abstractions;

namespace SQLGuardian.Domain;

/// <summary>
/// Rich metadata describing a rule for docs, dashboards, and configuration UIs.
/// Detection logic lives on <c>ISqlRule</c>; this is documentation/product surface.
/// </summary>
public sealed class RuleMetadata
{
    public required string RuleId { get; init; }

    public required string Title { get; init; }

    public required RuleCategory Category { get; init; }

    public required Severity DefaultSeverity { get; init; }

    public required string Description { get; init; }

    public string? Explanation { get; init; }

    public string? LearnMoreUrl { get; init; }

    public string? EstimatedImpact { get; init; }

    public bool AutoFixSupported { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>e.g. SQL Server 2016+, Azure SQL.</summary>
    public IReadOnlyList<string> DatabaseCompatibility { get; init; } = ["SQL Server"];
}
