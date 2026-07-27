using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using SQLGuardian.LanguageServer.Analysis;

namespace SQLGuardian.LanguageServer.Handlers;

public sealed class TextDocumentHandler : TextDocumentSyncHandlerBase
{
    private static readonly TextDocumentSelector SqlSelector = new(
        new TextDocumentFilter { Language = "sql" },
        new TextDocumentFilter { Pattern = "**/*.sql" });

    private readonly DocumentStore _store;
    private readonly DiagnosticPublisher _publisher;

    public TextDocumentHandler(DocumentStore store, DiagnosticPublisher publisher)
    {
        _store = store;
        _publisher = publisher;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
        new(uri, "sql");

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        var key = request.TextDocument.Uri.ToString();
        _store.Upsert(key, request.TextDocument.Text, request.TextDocument.Version);
        _publisher.Publish(request.TextDocument.Uri, request.TextDocument.Text);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var change = request.ContentChanges.LastOrDefault();
        if (change?.Text is null)
        {
            return Unit.Task;
        }

        var key = request.TextDocument.Uri.ToString();
        _store.Upsert(key, change.Text, request.TextDocument.Version);
        _publisher.Publish(request.TextDocument.Uri, change.Text);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        var key = request.TextDocument.Uri.ToString();
        if (!string.IsNullOrEmpty(request.Text))
        {
            _store.Upsert(key, request.Text);
            _publisher.Publish(request.TextDocument.Uri, request.Text);
            return Unit.Task;
        }

        if (_store.TryGet(key, out var state))
        {
            _publisher.Publish(request.TextDocument.Uri, state.Text);
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        var key = request.TextDocument.Uri.ToString();
        _store.Remove(key);
        _publisher.Clear(request.TextDocument.Uri);
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = SqlSelector,
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions { IncludeText = true }
        };
}
