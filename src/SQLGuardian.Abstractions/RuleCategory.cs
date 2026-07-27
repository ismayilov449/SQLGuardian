namespace SQLGuardian.Abstractions;

/// <summary>
/// Functional category for a rule. Mirrors SonarQube-style grouping.
/// </summary>
public enum RuleCategory
{
    Performance,
    Security,
    Readability,
    Concurrency,
    Maintainability,
    Correctness,
    BestPractices,
    Indexing,
    Statistics,
    ExecutionPlan
}
