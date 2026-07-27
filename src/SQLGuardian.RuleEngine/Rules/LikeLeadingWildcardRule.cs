using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom;
using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0005 — LIKE with leading wildcard.</summary>
public sealed class LikeLeadingWildcardRule : SqlRuleBase
{
    public override string RuleId => "SQLG0005";
    public override string Title => "LIKE with leading wildcard";
    public override string Description =>
        "LIKE patterns that start with % or _ are typically non-sargable.";
    public override Severity Severity => Severity.Medium;
    public override RuleCategory Category => RuleCategory.Performance;
    public override IReadOnlyList<string> Tags { get; } = ["like", "sargable", "index"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        foreach (var predicate in PredicateVisitor.Collect(fragment).Predicates)
        {
            if (predicate.Kind != PredicateKind.Like || predicate.PatternLiteral is null)
            {
                continue;
            }

            if (predicate.PatternLiteral.Length > 0
                && predicate.PatternLiteral[0] is '%' or '_')
            {
                issues.Add(CreateIssue(
                    context,
                    predicate.Location,
                    $"LIKE pattern '{predicate.PatternLiteral}' starts with a wildcard and may prevent index seeks.",
                    "Avoid a leading wildcard, or use full-text search for contains matching."));
            }
        }
    }
}
