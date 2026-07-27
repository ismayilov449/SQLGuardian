using SQLGuardian.Abstractions.Schema;
using SQLGuardian.Schema;

namespace SQLGuardian.Schema.Tests;

public class SchemaSnapshotCacheTests
{
    [Fact]
    public void GetOrAdd_ReusesCompatibleSnapshot()
    {
        var cache = new SchemaSnapshotCache(TimeSpan.FromMinutes(5));
        var loads = 0;

        SchemaSnapshot Loader(SchemaLoadProfile profile)
        {
            loads++;
            return new SchemaSnapshot(
                "Demo",
                [new TableSchema { Schema = "dbo", Name = "T", ApproximateRowCount = 1 }],
                DateTimeOffset.UtcNow);
        }

        var first = cache.GetOrAdd("Server=.;Database=Demo;Trusted_Connection=True", SchemaLoadProfile.CatalogScan, Loader);
        var second = cache.GetOrAdd("Server=.;Database=Demo;Trusted_Connection=True", SchemaLoadProfile.CatalogScan, Loader);

        Assert.Same(first, second);
        Assert.Equal(1, loads);
    }

    [Fact]
    public void Full_Satisfies_CatalogScan_WithoutReload()
    {
        var cache = new SchemaSnapshotCache(TimeSpan.FromMinutes(5));
        var loads = 0;

        SchemaSnapshot Loader(SchemaLoadProfile profile)
        {
            loads++;
            return new SchemaSnapshot("Demo", [], DateTimeOffset.UtcNow);
        }

        cache.GetOrAdd("Server=a;Database=b", SchemaLoadProfile.Full, Loader);
        cache.GetOrAdd("Server=a;Database=b", SchemaLoadProfile.CatalogScan, Loader);

        Assert.Equal(1, loads);
    }

    [Fact]
    public void CatalogScan_DoesNotSatisfy_Full()
    {
        var cache = new SchemaSnapshotCache(TimeSpan.FromMinutes(5));
        var loads = 0;

        SchemaSnapshot Loader(SchemaLoadProfile profile)
        {
            loads++;
            return new SchemaSnapshot("Demo", [], DateTimeOffset.UtcNow);
        }

        cache.GetOrAdd("Server=a;Database=b", SchemaLoadProfile.CatalogScan, Loader);
        cache.GetOrAdd("Server=a;Database=b", SchemaLoadProfile.Full, Loader);

        Assert.Equal(2, loads);
    }

    [Fact]
    public void Full_Satisfies_RowCountsOnly_WithoutReload()
    {
        var cache = new SchemaSnapshotCache(TimeSpan.FromMinutes(5));
        var loads = 0;

        SchemaSnapshot Loader(SchemaLoadProfile profile)
        {
            loads++;
            return new SchemaSnapshot("Demo", [], DateTimeOffset.UtcNow);
        }

        cache.GetOrAdd("Server=a;Database=b", SchemaLoadProfile.Full, Loader);
        cache.GetOrAdd("Server=a;Database=b", SchemaLoadProfile.RowCountsOnly, Loader);

        Assert.Equal(1, loads);
    }

    [Fact]
    public void RowCountsOnly_DoesNotSatisfy_CatalogScan()
    {
        var cache = new SchemaSnapshotCache(TimeSpan.FromMinutes(5));
        var loads = 0;

        SchemaSnapshot Loader(SchemaLoadProfile profile)
        {
            loads++;
            return new SchemaSnapshot("Demo", [], DateTimeOffset.UtcNow);
        }

        cache.GetOrAdd("Server=a;Database=b", SchemaLoadProfile.RowCountsOnly, Loader);
        cache.GetOrAdd("Server=a;Database=b", SchemaLoadProfile.CatalogScan, Loader);

        Assert.Equal(2, loads);
    }

    [Fact]
    public void Invalidate_ForcesReload()
    {
        var cache = new SchemaSnapshotCache(TimeSpan.FromMinutes(5));
        var loads = 0;

        SchemaSnapshot Loader(SchemaLoadProfile profile)
        {
            loads++;
            return new SchemaSnapshot("Demo", [], DateTimeOffset.UtcNow);
        }

        cache.GetOrAdd("Server=a;Database=b", SchemaLoadProfile.CatalogScan, Loader);
        cache.Invalidate();
        cache.GetOrAdd("Server=a;Database=b", SchemaLoadProfile.CatalogScan, Loader);

        Assert.Equal(2, loads);
    }
}
