using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>
/// Shared boilerplate for deterministic ScriptDom-based rules.
/// </summary>
public abstract class SqlRuleBase : ISqlRule
{
    public abstract string RuleId { get; }

    public abstract string Title { get; }

    public abstract string Description { get; }

    public abstract Severity Severity { get; }

    public abstract RuleCategory Category { get; }

    public virtual IReadOnlyList<string> Tags { get; } = [];

    public RuleResult Analyze(SqlAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.HasParseErrors || !ScriptDomSyntax.TryGetFragment(context, out var fragment))
        {
            return RuleResult.Empty;
        }

        var issues = new List<Issue>();
        AnalyzeCore(fragment, context, issues);
        return RuleResult.FromIssues(issues);
    }

    protected abstract void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues);

    protected Issue CreateIssue(
        SqlAnalysisContext context,
        SourceLocation location,
        string message,
        string? suggestion = null,
        string? suggestedSql = null) =>
        new()
        {
            RuleId = RuleId,
            Title = Title,
            Message = message,
            Severity = Severity,
            Category = Category,
            Location = location,
            Suggestion = suggestion,
            SuggestedSql = suggestedSql,
            FilePath = context.FilePath,
            Tags = Tags
        };
}
