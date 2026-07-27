namespace SQLGuardian.Abstractions.Schema;

/// <summary>
/// Loads catalog metadata (definitions + approximate row counts). Must not read table row data.
/// </summary>
public interface ISchemaProvider
{
    Task<SchemaSnapshot> LoadAsync(
        SchemaLoadOptions? options = null,
        CancellationToken cancellationToken = default);
}
