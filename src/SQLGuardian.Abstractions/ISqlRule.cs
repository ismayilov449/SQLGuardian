namespace SQLGuardian.Abstractions;

/// <summary>
/// Contract for every SQLGuardian rule.
/// Detection is deterministic. Rules never call an LLM.
/// </summary>
public interface ISqlRule
{
    /// <summary>Stable identifier, e.g. <c>SQLG0001</c>.</summary>
    string RuleId { get; }

    string Title { get; }

    string Description { get; }

    Severity Severity { get; }

    RuleCategory Category { get; }

    RuleResult Analyze(SqlAnalysisContext context);
}
