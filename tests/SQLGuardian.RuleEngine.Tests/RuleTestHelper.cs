using SQLGuardian.Abstractions;
using SQLGuardian.Domain;
using SQLGuardian.RuleEngine;

namespace SQLGuardian.RuleEngine.Tests;

internal static class RuleTestHelper
{
    public static AnalysisResult AnalyzeWith(ISqlRule rule, string sql, string filePath = "fixture.sql")
    {
        var engine = new SqlRuleEngine([rule]);
        var report = engine.Analyze(sql, filePath);
        return new AnalysisResult(report, report.Issues.Where(i => i.RuleId == rule.RuleId).ToList());
    }

    public static void AssertHasIssue(ISqlRule rule, string sql)
    {
        var result = AnalyzeWith(rule, sql);
        Assert.True(result.MatchingIssues.Count > 0,
            $"Expected {rule.RuleId} to fire. ParseErrors: {string.Join("; ", result.Report.ParseErrors)}");
    }

    public static void AssertNoIssue(ISqlRule rule, string sql)
    {
        var result = AnalyzeWith(rule, sql);
        Assert.True(result.Report.ParseErrors.Count == 0,
            $"Unexpected parse errors: {string.Join("; ", result.Report.ParseErrors)}");
        Assert.Empty(result.MatchingIssues);
    }

    internal sealed record AnalysisResult(AnalysisReport Report, IReadOnlyList<Issue> MatchingIssues);
}
