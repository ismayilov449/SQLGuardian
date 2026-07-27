namespace SQLGuardian.Abstractions.Schema;

public sealed class TableSchema
{
    public required string Schema { get; init; }

    public required string Name { get; init; }

    /// <summary>Approximate row count from partition stats (not a table scan).</summary>
    public long ApproximateRowCount { get; init; }

    public IReadOnlyList<ColumnSchema> Columns { get; init; } = [];

    public IReadOnlyList<IndexSchema> Indexes { get; init; } = [];

    /// <summary>Foreign keys defined on this table (child / referencing side).</summary>
    public IReadOnlyList<ForeignKeySchema> ForeignKeys { get; init; } = [];

    public string QualifiedName => $"{Schema}.{Name}";
}
