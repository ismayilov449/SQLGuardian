using SQLGuardian.Abstractions.Schema;

namespace SQLGuardian.Schema.Tests;

public class SchemaIndexHelpersTests
{
    [Fact]
    public void HasLeadingKeyIndex_MatchesPrefix()
    {
        var table = new TableSchema
        {
            Schema = "dbo",
            Name = "Orders",
            ApproximateRowCount = 10_000,
            Indexes =
            [
                new IndexSchema
                {
                    Name = "IX_Orders_CustomerId_OrderDate",
                    KeyColumns = ["CustomerId", "OrderDate"]
                }
            ]
        };

        Assert.True(SchemaIndexHelpers.HasLeadingKeyIndex(table, ["CustomerId"]));
        Assert.True(SchemaIndexHelpers.HasLeadingKeyIndex(table, ["CustomerId", "OrderDate"]));
        Assert.False(SchemaIndexHelpers.HasLeadingKeyIndex(table, ["OrderDate"]));
        Assert.False(SchemaIndexHelpers.HasLeadingKeyIndex(table, ["CustomerId", "Status"]));
    }
}

public class SchemaSnapshotTests
{
    [Fact]
    public void FindTable_DefaultsMissingSchemaToDbo_AndFallsBackToUniqueName()
    {
        var snapshot = new SchemaSnapshot(
            "Demo",
            [
                new TableSchema { Schema = "dbo", Name = "Users", ApproximateRowCount = 5 },
                new TableSchema { Schema = "sales", Name = "Orders", ApproximateRowCount = 9 }
            ],
            DateTimeOffset.UtcNow);

        Assert.Equal("dbo", snapshot.FindTable(null, "Users")!.Schema);
        Assert.Equal("sales", snapshot.FindTable(null, "Orders")!.Schema);
        Assert.Null(snapshot.FindTable("hr", "Users"));
    }
}
