using System.Collections.Concurrent;

namespace SQLGuardian.LanguageServer.Analysis;

/// <summary>
/// In-memory open document texts keyed by URI.
/// </summary>
public sealed class DocumentStore
{
    private readonly ConcurrentDictionary<string, DocumentState> _documents =
        new(StringComparer.OrdinalIgnoreCase);

    public void Upsert(string uri, string text, int? version = null) =>
        _documents[uri] = new DocumentState(text, version);

    public bool TryGet(string uri, out DocumentState state) =>
        _documents.TryGetValue(uri, out state!);

    public bool Remove(string uri) => _documents.TryRemove(uri, out _);

    public IReadOnlyCollection<string> Uris => _documents.Keys.ToList();
}

public sealed record DocumentState(string Text, int? Version);
