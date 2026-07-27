using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0006 — CROSS JOIN usage.</summary>
public sealed class CrossJoinRule : SqlRuleBase
{
    public override string RuleId => "SQLG0006";
    public override string Title => "CROSS JOIN usage";
    public override string Description =>
        "CROSS JOIN produces a Cartesian product and is often unintentional.";
    public override Severity Severity => Severity.Medium;
    public override RuleCategory Category => RuleCategory.Performance;
    public override IReadOnlyList<string> Tags { get; } = ["join", "cross-join", "cardinality"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        foreach (var join in JoinVisitor.Collect(fragment).Joins)
        {
            if (join.Kind != SqlJoinKind.CrossJoin)
            {
                continue;
            }

            issues.Add(CreateIssue(
                context,
                join.Location,
                "CROSS JOIN creates a Cartesian product between row sets.",
                "Prefer an INNER JOIN with an explicit ON predicate when you intend to match rows.",
                """
                -- Example rewrite (replace Key columns with your real join keys):
                SELECT ...
                FROM dbo.LeftTable AS l
                INNER JOIN dbo.RightTable AS r
                    ON r.LeftId = l.Id;
                """));
        }
    }
}
