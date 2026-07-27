namespace SQLGuardian.Abstractions.Schema;

public sealed class ColumnSchema
{
    public required string Name { get; init; }

    public int Ordinal { get; init; }

    public bool IsNullable { get; init; }

    public required string DataType { get; init; }
}
