using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.RuleEngine;
using SQLGuardian.RuleEngine.Rules;

namespace SQLGuardian.RuleEngine.Tests;

public class SecondPackRuleTests
{
    [Fact]
    public void Catalog_NowIncludes_Sqlg0013_Through_0022()
    {
        var rules = RuleCatalog.CreateDefault();
        Assert.Equal(22, rules.Count);
        Assert.Contains(rules, r => r.RuleId == "SQLG0013");
        Assert.Contains(rules, r => r.RuleId == "SQLG0022");
    }
}

public class NonSargableFunctionOnColumnRuleTests
{
    private readonly NonSargableFunctionOnColumnRule _rule = new();

    [Fact]
    public void Flags_YearOnColumn() =>
        RuleTestHelper.AssertHasIssue(_rule, "SELECT Id FROM dbo.Orders WHERE YEAR(OrderDate) = 2024;");

    [Fact]
    public void Allows_RangeOnColumn() =>
        RuleTestHelper.AssertNoIssue(_rule, "SELECT Id FROM dbo.Orders WHERE OrderDate >= '20240101';");
}

public class DynamicSqlConcatenationRuleTests
{
    private readonly DynamicSqlConcatenationRule _rule = new();

    [Fact]
    public void Flags_ExecConcat() =>
        RuleTestHelper.AssertHasIssue(_rule, "EXEC(N'SELECT * FROM ' + @t);");

    [Fact]
    public void Allows_SpExecutesqlLiteral() =>
        RuleTestHelper.AssertNoIssue(_rule, "EXEC sys.sp_executesql N'SELECT 1';");
}

public class MissingSetNocountOnRuleTests
{
    private readonly MissingSetNocountOnRule _rule = new();

    [Fact]
    public void Flags_ProcedureWithoutNocount() =>
        RuleTestHelper.AssertHasIssue(_rule, """
            CREATE PROCEDURE dbo.GetUsers AS
            BEGIN
              SELECT Id FROM dbo.Users;
            END
            """);

    [Fact]
    public void Allows_ProcedureWithNocount() =>
        RuleTestHelper.AssertNoIssue(_rule, """
            CREATE PROCEDURE dbo.GetUsers AS
            BEGIN
              SET NOCOUNT ON;
              SELECT Id FROM dbo.Users;
            END
            """);
}

public class NotInSubqueryRuleTests
{
    private readonly NotInSubqueryRule _rule = new();

    [Fact]
    public void Flags_NotInSubquery() =>
        RuleTestHelper.AssertHasIssue(_rule, "SELECT Id FROM dbo.Users WHERE Id NOT IN (SELECT UserId FROM dbo.Orders);");

    [Fact]
    public void Allows_NotInList() =>
        RuleTestHelper.AssertNoIssue(_rule, "SELECT Id FROM dbo.Users WHERE Id NOT IN (1, 2, 3);");
}

public class InSubqueryPreferExistsRuleTests
{
    private readonly InSubqueryPreferExistsRule _rule = new();

    [Fact]
    public void Flags_InSubquery() =>
        RuleTestHelper.AssertHasIssue(_rule, "SELECT Id FROM dbo.Users WHERE Id IN (SELECT UserId FROM dbo.Orders);");
}

public class TruncateTableRuleTests
{
    private readonly TruncateTableRule _rule = new();

    [Fact]
    public void Flags_Truncate() =>
        RuleTestHelper.AssertHasIssue(_rule, "TRUNCATE TABLE dbo.Users;");
}

public class XpCmdshellRuleTests
{
    private readonly XpCmdshellRule _rule = new();

    [Fact]
    public void Flags_XpCmdshell() =>
        RuleTestHelper.AssertHasIssue(_rule, "EXEC xp_cmdshell 'dir';");
}

public class OpenRowsetRuleTests
{
    private readonly OpenRowsetRule _rule = new();

    [Fact]
    public void Flags_OpenRowset() =>
        RuleTestHelper.AssertHasIssue(_rule,
            "SELECT * FROM OPENROWSET('SQLNCLI', 'Server=.;Trusted_Connection=yes;', 'SELECT 1');");
}

public class ImplicitConversionRiskRuleTests
{
    private readonly ImplicitConversionRiskRule _rule = new();

    [Fact]
    public void Flags_VarcharColumnComparedToInt()
    {
        var schema = new SchemaSnapshot(
            "Demo",
            [
                new TableSchema
                {
                    Schema = "dbo",
                    Name = "Items",
                    ApproximateRowCount = 10_000,
                    Columns =
                    [
                        new ColumnSchema { Name = "Id", Ordinal = 1, IsNullable = false, DataType = "int" },
                        new ColumnSchema { Name = "Code", Ordinal = 2, IsNullable = false, DataType = "varchar" }
                    ]
                }
            ],
            DateTimeOffset.UtcNow);

        var engine = new SqlRuleEngine([_rule]);
        var report = engine.Analyze("SELECT Id FROM dbo.Items WHERE Code = 123;", "q.sql", schema);
        Assert.Contains(report.Issues, i => i.RuleId == "SQLG0018");
    }

    [Fact]
    public void NoOps_WithoutSchema() =>
        RuleTestHelper.AssertNoIssue(_rule, "SELECT Id FROM dbo.Items WHERE Code = 123;");
}

public class NonLeadingIndexKeyFilterRuleTests
{
    private readonly NonLeadingIndexKeyFilterRule _rule = new();

    [Fact]
    public void Flags_NonLeadingKeyFilter()
    {
        var schema = new SchemaSnapshot(
            "Demo",
            [
                new TableSchema
                {
                    Schema = "dbo",
                    Name = "Orders",
                    ApproximateRowCount = 50_000,
                    Columns =
                    [
                        new ColumnSchema { Name = "CustomerId", Ordinal = 1, IsNullable = false, DataType = "int" },
                        new ColumnSchema { Name = "StatusId", Ordinal = 2, IsNullable = false, DataType = "int" }
                    ],
                    Indexes =
                    [
                        new IndexSchema
                        {
                            Name = "IX_Orders_Customer_Status",
                            KeyColumns = ["CustomerId", "StatusId"]
                        }
                    ]
                }
            ],
            DateTimeOffset.UtcNow);

        var engine = new SqlRuleEngine([_rule]);
        var report = engine.Analyze("SELECT 1 FROM dbo.Orders WHERE StatusId = 2;", "q.sql", schema);
        Assert.Contains(report.Issues, i => i.RuleId == "SQLG0022");
    }
}
