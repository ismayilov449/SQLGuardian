using System.Text;
using SQLGuardian.Abstractions;

namespace SQLGuardian.RuleEngine;

/// <summary>
/// Builds a single copy-pasteable recommendation script from deterministic issue fixes.
/// </summary>
public static class RecommendationComposer
{
    public static string? Compose(IReadOnlyList<Issue> issues, string? sourceFileName = null)
    {
        ArgumentNullException.ThrowIfNull(issues);

        var blocks = issues
            .Where(i => !string.IsNullOrWhiteSpace(i.SuggestedSql))
            .GroupBy(i => NormalizeSql(i.SuggestedSql!), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(i => i.RuleId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Location.StartLine)
            .ToList();

        if (blocks.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine("-- SQLGuardian recommended fixes (deterministic — review before applying)");
        if (!string.IsNullOrWhiteSpace(sourceFileName))
        {
            sb.AppendLine($"-- Source: {sourceFileName}");
        }

        sb.AppendLine();

        foreach (var issue in blocks)
        {
            sb.AppendLine($"-- {issue.RuleId}: {issue.Title}");
            if (!string.IsNullOrWhiteSpace(issue.Suggestion))
            {
                sb.AppendLine($"-- {issue.Suggestion}");
            }

            sb.AppendLine(issue.SuggestedSql!.Trim());
            sb.AppendLine("GO");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string NormalizeSql(string sql) =>
        string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
