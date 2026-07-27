namespace SQLGuardian.Abstractions;

/// <summary>
/// Outcome of running a single rule against one analysis context.
/// </summary>
public sealed class RuleResult
{
    public IReadOnlyList<Issue> Issues { get; }

    public RuleResult(IReadOnlyList<Issue> issues)
    {
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }

    public static RuleResult Empty { get; } = new([]);

    public static RuleResult FromIssues(params Issue[] issues) => new(issues);

    public static RuleResult FromIssues(IEnumerable<Issue> issues) => new(issues.ToList());
}
