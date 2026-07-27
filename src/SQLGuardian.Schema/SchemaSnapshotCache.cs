using SQLGuardian.Abstractions.Schema;

namespace SQLGuardian.Schema;

/// <summary>
/// Short-lived in-process cache so Scan / Analyze do not reload the catalog every click.
/// </summary>
public sealed class SchemaSnapshotCache
{
    private readonly object _gate = new();
    private readonly TimeSpan _ttl;
    private Entry? _entry;

    public SchemaSnapshotCache(TimeSpan? ttl = null)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(5);
    }

    public SchemaSnapshot GetOrAdd(
        string connectionString,
        SchemaLoadProfile profile,
        Func<SchemaLoadProfile, SchemaSnapshot> loader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(loader);

        var key = NormalizeKey(connectionString);
        lock (_gate)
        {
            if (_entry is { } hit
                && hit.Key == key
                && hit.ExpiresAt > DateTimeOffset.UtcNow
                && IsCompatible(hit.Profile, profile))
            {
                return hit.Snapshot;
            }
        }

        // Load outside the lock (SQL I/O). Concurrent callers may double-load once — acceptable.
        var snapshot = loader(profile);

        lock (_gate)
        {
            if (_entry is { } hit
                && hit.Key == key
                && hit.ExpiresAt > DateTimeOffset.UtcNow
                && IsCompatible(hit.Profile, profile))
            {
                return hit.Snapshot;
            }

            // Keep a Full snapshot if we already have one and this load was CatalogScan.
            if (_entry is { } existing
                && existing.Key == key
                && existing.ExpiresAt > DateTimeOffset.UtcNow
                && existing.Profile == SchemaLoadProfile.Full
                && profile == SchemaLoadProfile.CatalogScan)
            {
                return existing.Snapshot;
            }

            _entry = new Entry(key, profile, snapshot, DateTimeOffset.UtcNow.Add(_ttl));
            return snapshot;
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _entry = null;
        }
    }

    /// <summary>
    /// Full satisfies CatalogScan and RowCountsOnly.
    /// CatalogScan does not satisfy Full or RowCountsOnly (missing most tables' counts when filtered).
    /// RowCountsOnly satisfies only itself (no FK/index data).
    /// </summary>
    private static bool IsCompatible(SchemaLoadProfile cached, SchemaLoadProfile requested)
    {
        if (cached == requested)
        {
            return true;
        }

        return cached == SchemaLoadProfile.Full
            && (requested == SchemaLoadProfile.CatalogScan
                || requested == SchemaLoadProfile.RowCountsOnly);
    }

    private static string NormalizeKey(string connectionString) =>
        string.Join(
            ';',
            connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase));

    private sealed record Entry(
        string Key,
        SchemaLoadProfile Profile,
        SchemaSnapshot Snapshot,
        DateTimeOffset ExpiresAt);
}
