using SQLGuardian.Abstractions.Schema;
using SQLGuardian.RuleEngine;

namespace SQLGuardian.RuleEngine.Tests;

public class LargeTableReadPrecheckTests
{
    private static SchemaSnapshot Schema() =>
        new(
            "DemoDb",
            [
                new TableSchema { Schema = "dbo", Name = "Products", ApproximateRowCount = 19_000_000 },
                new TableSchema { Schema = "dbo", Name = "Tiny", ApproximateRowCount = 10 },
                new TableSchema { Schema = "sales", Name = "Orders", ApproximateRowCount = 2_500_000 }
            ],
            DateTimeOffset.UtcNow);

    [Fact]
    public void Flags_SelectStar_On_Large_Table()
    {
        var warnings = LargeTableReadPrecheck.Evaluate(
            "SELECT * FROM dbo.Products;",
            Schema());

        var warning = Assert.Single(warnings);
        Assert.Equal("Products", warning.Table);
        Assert.Equal(LargeTableReadKind.SelectStar, warning.Kind);
        Assert.Equal(19_000_000, warning.ApproximateRowCount);
        Assert.Contains("SELECT *", warning.Message);
    }

    [Fact]
    public void Flags_Unbounded_Select_On_Large_Table()
    {
        var warnings = LargeTableReadPrecheck.Evaluate(
            "SELECT Id, Name FROM dbo.Products;",
            Schema());

        var warning = Assert.Single(warnings);
        Assert.Equal(LargeTableReadKind.UnboundedSelect, warning.Kind);
    }

    [Fact]
    public void Ignores_Filtered_Select_Without_Star()
    {
        var warnings = LargeTableReadPrecheck.Evaluate(
            "SELECT Id FROM dbo.Products WHERE Id = 1;",
            Schema());

        Assert.Empty(warnings);
    }

    [Fact]
    public void Ignores_Top_Without_Star()
    {
        var warnings = LargeTableReadPrecheck.Evaluate(
            "SELECT TOP (100) Id FROM dbo.Products;",
            Schema());

        Assert.Empty(warnings);
    }

    [Fact]
    public void Ignores_Small_Tables()
    {
        var warnings = LargeTableReadPrecheck.Evaluate(
            "SELECT * FROM dbo.Tiny;",
            Schema());

        Assert.Empty(warnings);
    }

    [Fact]
    public void Respects_Custom_Threshold()
    {
        var warnings = LargeTableReadPrecheck.Evaluate(
            "SELECT * FROM dbo.Tiny;",
            Schema(),
            rowThreshold: 5);

        Assert.Single(warnings);
    }

    [Fact]
    public void Prefers_SelectStar_Over_Unbounded_For_Same_Table()
    {
        var warnings = LargeTableReadPrecheck.Evaluate(
            "SELECT * FROM dbo.Products; SELECT Id FROM dbo.Products;",
            Schema());

        var warning = Assert.Single(warnings);
        Assert.Equal(LargeTableReadKind.SelectStar, warning.Kind);
    }

    [Fact]
    public void Flags_Multiple_Large_Tables()
    {
        var warnings = LargeTableReadPrecheck.Evaluate(
            "SELECT * FROM dbo.Products; SELECT * FROM sales.Orders;",
            Schema());

        Assert.Equal(2, warnings.Count);
        Assert.Equal("Products", warnings[0].Table);
        Assert.Equal("Orders", warnings[1].Table);
    }
}
