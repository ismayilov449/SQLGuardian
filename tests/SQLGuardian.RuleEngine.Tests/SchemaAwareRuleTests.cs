using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.RuleEngine;
using SQLGuardian.RuleEngine.Rules;

namespace SQLGuardian.RuleEngine.Tests;

public class MissingForeignKeyIndexRuleTests
{
    private readonly MissingForeignKeyIndexRule _rule = new();

    [Fact]
    public void Catalog_Flags_FkWithoutIndex_WhenRowsAboveThreshold()
    {
        var schema = BuildSchema(rowCount: 50_000, indexed: false);
        var engine = new SqlRuleEngine([_rule]);
        var report = engine.AnalyzeCatalog(schema);

        Assert.Contains(report.Issues, i => i.RuleId == "SQLG0011" && i.Message.Contains("FK_Orders_Customers"));
    }

    [Fact]
    public void Catalog_Skips_WhenLeadingIndexExists()
    {
        var schema = BuildSchema(rowCount: 50_000, indexed: true);
        var engine = new SqlRuleEngine([_rule]);
        var report = engine.AnalyzeCatalog(schema);

        Assert.DoesNotContain(report.Issues, i => i.RuleId == "SQLG0011");
    }

    [Fact]
    public void Catalog_Skips_SmallTables()
    {
        var schema = BuildSchema(rowCount: 10, indexed: false);
        var engine = new SqlRuleEngine([_rule]);
        var report = engine.AnalyzeCatalog(schema);

        Assert.Empty(report.Issues);
    }

    [Fact]
    public void Script_OnlyFlags_ReferencedTables()
    {
        var schema = BuildSchema(rowCount: 50_000, indexed: false);
        var engine = new SqlRuleEngine([_rule]);
        var report = engine.Analyze("SELECT 1 FROM dbo.Unrelated;", "q.sql", schema);

        Assert.Empty(report.Issues);

        report = engine.Analyze("SELECT 1 FROM dbo.Orders;", "q.sql", schema);
        Assert.Contains(report.Issues, i => i.RuleId == "SQLG0011");
    }

    private static SchemaSnapshot BuildSchema(long rowCount, bool indexed)
    {
        var indexes = indexed
            ?
            [
                new IndexSchema
                {
                    Name = "IX_Orders_CustomerId",
                    KeyColumns = ["CustomerId"]
                }
            ]
            : Array.Empty<IndexSchema>();

        return new SchemaSnapshot(
            "Demo",
            [
                new TableSchema
                {
                    Schema = "dbo",
                    Name = "Orders",
                    ApproximateRowCount = rowCount,
                    Indexes = indexes,
                    ForeignKeys =
                    [
                        new ForeignKeySchema
                        {
                            Name = "FK_Orders_Customers",
                            ParentSchema = "dbo",
                            ParentTable = "Orders",
                            ParentColumns = ["CustomerId"],
                            ReferencedSchema = "dbo",
                            ReferencedTable = "Customers",
                            ReferencedColumns = ["Id"]
                        }
                    ]
                },
                new TableSchema
                {
                    Schema = "dbo",
                    Name = "Unrelated",
                    ApproximateRowCount = rowCount
                }
            ],
            DateTimeOffset.UtcNow);
    }
}

public class UnindexedJoinColumnRuleTests
{
    private readonly UnindexedJoinColumnRule _rule = new();

    [Fact]
    public void Flags_UnindexedJoinColumn_WhenSchemaPresent()
    {
        var schema = new SchemaSnapshot(
            "Demo",
            [
                new TableSchema
                {
                    Schema = "dbo",
                    Name = "Orders",
                    ApproximateRowCount = 20_000,
                    Indexes =
                    [
                        new IndexSchema
                        {
                            Name = "PK_Orders",
                            IsPrimaryKey = true,
                            IsUnique = true,
                            KeyColumns = ["Id"]
                        }
                    ]
                },
                new TableSchema
                {
                    Schema = "dbo",
                    Name = "Customers",
                    ApproximateRowCount = 5_000,
                    Indexes =
                    [
                        new IndexSchema
                        {
                            Name = "PK_Customers",
                            IsPrimaryKey = true,
                            IsUnique = true,
                            KeyColumns = ["Id"]
                        }
                    ]
                }
            ],
            DateTimeOffset.UtcNow);

        var engine = new SqlRuleEngine([_rule]);
        var report = engine.Analyze("""
            SELECT o.Id
            FROM dbo.Orders AS o
            INNER JOIN dbo.Customers AS c ON o.CustomerId = c.Id;
            """, "join.sql", schema);

        Assert.Contains(report.Issues, i =>
            i.RuleId == "SQLG0012" && i.Message.Contains("Orders.CustomerId", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(report.Issues, i =>
            i.Message.Contains("Customers.Id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Noops_WithoutSchema()
    {
        RuleTestHelper.AssertNoIssue(
            _rule,
            """
            SELECT o.Id
            FROM dbo.Orders AS o
            INNER JOIN dbo.Customers AS c ON o.CustomerId = c.Id;
            """);
    }
}
