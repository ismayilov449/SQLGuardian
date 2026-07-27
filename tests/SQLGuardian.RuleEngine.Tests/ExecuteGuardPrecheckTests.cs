using SQLGuardian.Abstractions.Schema;
using SQLGuardian.RuleEngine;

namespace SQLGuardian.RuleEngine.Tests;

public class ExecuteGuardPrecheckTests
{
    private static SchemaSnapshot Schema() =>
        new(
            "DemoDb",
            [
                new TableSchema { Schema = "dbo", Name = "Products", ApproximateRowCount = 19_000_000 },
                new TableSchema { Schema = "dbo", Name = "Tiny", ApproximateRowCount = 10 },
                new TableSchema { Schema = "dbo", Name = "Orders", ApproximateRowCount = 2_500_000 }
            ],
            DateTimeOffset.UtcNow);

    [Fact]
    public void Flags_Update_Without_Where()
    {
        var result = ExecuteGuardPrecheck.Evaluate("UPDATE dbo.Tiny SET Name = N'x';");

        var warning = Assert.Single(result.MissingWhere);
        Assert.Equal("UPDATE", warning.StatementKind);
        Assert.Contains("WHERE", warning.Message);
    }

    [Fact]
    public void Flags_Delete_Without_Where()
    {
        var result = ExecuteGuardPrecheck.Evaluate("DELETE FROM dbo.Products;");

        var warning = Assert.Single(result.MissingWhere);
        Assert.Equal("DELETE", warning.StatementKind);
    }

    [Fact]
    public void Ignores_Update_With_Where()
    {
        var result = ExecuteGuardPrecheck.Evaluate("UPDATE dbo.Tiny SET Name = N'x' WHERE Id = 1;");

        Assert.Empty(result.MissingWhere);
    }

    [Fact]
    public void Flags_Large_Join_Above_Threshold()
    {
        var result = ExecuteGuardPrecheck.Evaluate(
            """
            SELECT p.Id, o.Id
            FROM dbo.Tiny AS t
            INNER JOIN dbo.Products AS p ON p.Id = t.Id;
            """,
            Schema(),
            rowThreshold: 1_000_000);

        var warning = Assert.Single(result.LargeJoins);
        Assert.Equal("Products", warning.Table);
        Assert.Contains("Join involves", warning.Message);
    }

    [Fact]
    public void Ignores_Join_Of_Small_Tables()
    {
        var result = ExecuteGuardPrecheck.Evaluate(
            """
            SELECT a.Id
            FROM dbo.Tiny AS a
            INNER JOIN dbo.Tiny AS b ON a.Id = b.Id;
            """,
            Schema(),
            rowThreshold: 1_000_000);

        Assert.Empty(result.LargeJoins);
    }

    [Fact]
    public void Ignores_Large_Join_Already_With_Nolock()
    {
        var result = ExecuteGuardPrecheck.Evaluate(
            """
            SELECT p.Id
            FROM dbo.Tiny AS t
            INNER JOIN dbo.Products AS p WITH (NOLOCK) ON p.Id = t.Id;
            """,
            Schema());

        Assert.Empty(result.LargeJoins);
    }

    [Fact]
    public void ApplyNolockHints_Inserts_Hint()
    {
        const string sql =
            """
            SELECT p.Id
            FROM dbo.Tiny AS t
            INNER JOIN dbo.Products AS p ON p.Id = t.Id;
            """;

        var result = ExecuteGuardPrecheck.Evaluate(sql, Schema());
        Assert.NotEmpty(result.LargeJoins);

        var rewritten = ExecuteGuardPrecheck.ApplyNolockHints(sql, result.LargeJoins);
        Assert.Contains("WITH (NOLOCK)", rewritten, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Products", rewritten, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingWhere_Works_Without_Schema()
    {
        var result = ExecuteGuardPrecheck.Evaluate("DELETE dbo.Anything;");

        Assert.Single(result.MissingWhere);
        Assert.Empty(result.LargeReads);
        Assert.Empty(result.LargeJoins);
    }
}
