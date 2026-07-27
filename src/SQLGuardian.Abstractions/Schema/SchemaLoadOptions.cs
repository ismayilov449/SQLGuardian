namespace SQLGuardian.Abstractions.Schema;

/// <summary>
/// Controls how much catalog metadata is loaded from SQL Server.
/// </summary>
public enum SchemaLoadProfile
{
    /// <summary>
    /// Tables, columns, indexes, FKs, row counts — needed for script recommendations (e.g. SELECT * expansion).
    /// </summary>
    Full = 0,

    /// <summary>
    /// Fast path for catalog FK-index scan: FKs + row counts + indexes on FK parent tables only. No columns.
    /// </summary>
    CatalogScan = 1,

    /// <summary>
    /// Fast path for pre-execute guards: all user tables + approximate row counts only.
    /// </summary>
    RowCountsOnly = 2
}

public sealed class SchemaLoadOptions
{
    public static SchemaLoadOptions Full { get; } = new() { Profile = SchemaLoadProfile.Full };

    public static SchemaLoadOptions CatalogScan { get; } = new() { Profile = SchemaLoadProfile.CatalogScan };

    public static SchemaLoadOptions RowCountsOnly { get; } = new() { Profile = SchemaLoadProfile.RowCountsOnly };

    public SchemaLoadProfile Profile { get; init; } = SchemaLoadProfile.Full;

    /// <summary>Command timeout in seconds for catalog queries.</summary>
    public int CommandTimeoutSeconds { get; init; } = 60;
}
