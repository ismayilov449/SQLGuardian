using MediatR;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using SQLGuardian.LanguageServer.Analysis;

namespace SQLGuardian.LanguageServer.Handlers;

/// <summary>
/// Handles workspace/executeCommand for on-demand analysis.
/// </summary>
public sealed class AnalyzeCommandHandler : IExecuteCommandHandler
{
    public const string CommandName = "sqlguardian.analyze";

    private readonly DocumentStore _store;
    private readonly DiagnosticPublisher _publisher;
    private readonly SqlAnalysisService _analysis;

    public AnalyzeCommandHandler(
        DocumentStore store,
        DiagnosticPublisher publisher,
        SqlAnalysisService analysis)
    {
        _store = store;
        _publisher = publisher;
        _analysis = analysis;
    }

    public Task<Unit> Handle(ExecuteCommandParams request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Command, CommandName, StringComparison.Ordinal))
        {
            return Unit.Task;
        }

        var configPath = ReadArg(request.Arguments, 0);
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            _analysis.ReloadConfiguration(configPath);
        }

        var uriArg = ReadArg(request.Arguments, 1);
        if (!string.IsNullOrWhiteSpace(uriArg))
        {
            AnalyzeOne(uriArg);
            return Unit.Task;
        }

        foreach (var uri in _store.Uris)
        {
            AnalyzeOne(uri);
        }

        return Unit.Task;
    }

    private void AnalyzeOne(string uriString)
    {
        if (!_store.TryGet(uriString, out var state))
        {
            return;
        }

        var uri = DocumentUri.From(uriString);
        _publisher.Publish(uri, state.Text);
    }

    private static string? ReadArg(JArray? arguments, int index)
    {
        if (arguments is null || arguments.Count <= index)
        {
            return null;
        }

        var token = arguments[index];
        if (token is null || token.Type is JTokenType.Null or JTokenType.Undefined)
        {
            return null;
        }

        return token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
    }

    public ExecuteCommandRegistrationOptions GetRegistrationOptions(
        ExecuteCommandCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            Commands = new Container<string>(CommandName)
        };
}
