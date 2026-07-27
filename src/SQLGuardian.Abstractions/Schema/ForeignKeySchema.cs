namespace SQLGuardian.Abstractions.Schema;

public sealed class ForeignKeySchema
{
    public required string Name { get; init; }

    public required string ParentSchema { get; init; }

    public required string ParentTable { get; init; }

    public IReadOnlyList<string> ParentColumns { get; init; } = [];

    public required string ReferencedSchema { get; init; }

    public required string ReferencedTable { get; init; }

    public IReadOnlyList<string> ReferencedColumns { get; init; } = [];

    public string ParentQualifiedName => $"{ParentSchema}.{ParentTable}";

    public string ReferencedQualifiedName => $"{ReferencedSchema}.{ReferencedTable}";
}
