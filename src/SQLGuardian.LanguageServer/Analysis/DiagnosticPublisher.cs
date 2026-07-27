using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace SQLGuardian.LanguageServer.Analysis;

public sealed class DiagnosticPublisher
{
    private readonly ILanguageServerFacade _server;
    private readonly SqlAnalysisService _analysis;

    public DiagnosticPublisher(ILanguageServerFacade server, SqlAnalysisService analysis)
    {
        _server = server;
        _analysis = analysis;
    }

    public void Publish(DocumentUri uri, string text, string? filePath = null)
    {
        var path = filePath ?? UriToPath(uri) ?? uri.ToString();
        _analysis.EnsureConfigurationNear(path);
        var report = _analysis.Analyze(text, path);
        var diagnostics = DiagnosticMapper.Map(report);

        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(diagnostics.ToArray())
        });
    }

    public void Clear(DocumentUri uri) =>
        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = Array.Empty<Diagnostic>()
        });

    private static string? UriToPath(DocumentUri uri)
    {
        try
        {
            var path = uri.GetFileSystemPath();
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch
        {
            return null;
        }
    }
}
