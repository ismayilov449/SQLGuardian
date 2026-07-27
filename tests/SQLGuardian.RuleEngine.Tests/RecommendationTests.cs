using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.RuleEngine;
using SQLGuardian.RuleEngine.Rules;

namespace SQLGuardian.RuleEngine.Tests;

public class RecommendationComposerTests
{
    [Fact]
    public void Compose_BuildsCopyPasteScript_FromSuggestedSql()
    {
        var issues = new[]
        {
            new Issue
            {
                RuleId = "SQLG0012",
                Title = "Unindexed join column",
                Message = "missing index",
                Severity = Severity.Medium,
                Category = RuleCategory.Performance,
                Location = SourceLocation.Unknown,
                Suggestion = "Add index",
                SuggestedSql = IndexRecommendationSql.CreateNonclusteredIndex(
                    "dbo",
                    "DemoOrders",
                    ["CustomerId"])
            }
        };

        var script = RecommendationComposer.Compose(issues, "join.sql");

        Assert.NotNull(script);
        Assert.Contains("SQLG0012", script, StringComparison.Ordinal);
        Assert.Contains("CREATE NONCLUSTERED INDEX", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DemoOrders", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GO", script, StringComparison.Ordinal);
    }
}

public class SelectStarRecommendationTests
{
    [Fact]
    public void WithSchema_SuggestsExplicitColumnList()
    {
        var schema = new SchemaSnapshot(
            "Demo",
            [
                new TableSchema
                {
                    Schema = "dbo",
                    Name = "DemoOrders",
                    ApproximateRowCount = 2_000,
                    Columns =
                    [
                        new ColumnSchema { Name = "Id", Ordinal = 1, DataType = "int", IsNullable = false },
                        new ColumnSchema { Name = "CustomerId", Ordinal = 2, DataType = "int", IsNullable = false },
                        new ColumnSchema { Name = "Amount", Ordinal = 3, DataType = "decimal", IsNullable = false }
                    ]
                }
            ],
            DateTimeOffset.UtcNow);

        var engine = new SqlRuleEngine([new SelectStarRule()]);
        var report = engine.Analyze("SELECT * FROM dbo.DemoOrders;", "q.sql", schema);

        var issue = Assert.Single(report.Issues);
        Assert.Equal("SQLG0001", issue.RuleId);
        Assert.NotNull(issue.SuggestedSql);
        Assert.Contains("[Id]", issue.SuggestedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[CustomerId]", issue.SuggestedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Amount]", issue.SuggestedSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT *", issue.SuggestedSql!.Split("was:")[0], StringComparison.Ordinal);
        Assert.NotNull(report.RecommendedSql);
        Assert.Contains("[CustomerId]", report.RecommendedSql, StringComparison.OrdinalIgnoreCase);
    }
}

public class UnindexedJoinRecommendationTests
{
    [Fact]
    public void SuggestsCreateIndexSql()
    {
        var schema = new SchemaSnapshot(
            "Demo",
            [
                new TableSchema
                {
                    Schema = "dbo",
                    Name = "DemoOrders",
                    ApproximateRowCount = 20_000,
                    Indexes =
                    [
                        new IndexSchema
                        {
                            Name = "PK_DemoOrders",
                            IsPrimaryKey = true,
                            IsUnique = true,
                            KeyColumns = ["Id"]
                        }
                    ]
                },
                new TableSchema
                {
                    Schema = "dbo",
                    Name = "DemoCustomers",
                    ApproximateRowCount = 5_000,
                    Indexes =
                    [
                        new IndexSchema
                        {
                            Name = "PK_DemoCustomers",
                            IsPrimaryKey = true,
                            IsUnique = true,
                            KeyColumns = ["Id"]
                        }
                    ]
                }
            ],
            DateTimeOffset.UtcNow);

        var engine = new SqlRuleEngine([new UnindexedJoinColumnRule()]);
        var report = engine.Analyze("""
            SELECT o.Id
            FROM dbo.DemoOrders AS o
            INNER JOIN dbo.DemoCustomers AS c ON o.CustomerId = c.Id;
            """, "join.sql", schema);

        var issue = Assert.Single(report.Issues, i => i.RuleId == "SQLG0012");
        Assert.NotNull(issue.SuggestedSql);
        Assert.Contains("CREATE NONCLUSTERED INDEX", issue.SuggestedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CustomerId", issue.SuggestedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF NOT EXISTS", issue.SuggestedSql, StringComparison.OrdinalIgnoreCase);
    }
}
