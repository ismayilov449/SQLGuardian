using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using SQLGuardian.LanguageServer.Analysis;
using SQLGuardian.LanguageServer.Handlers;

namespace SQLGuardian.LanguageServer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // stdout is reserved for LSP JSON-RPC. Log only to stderr.
        await using var stderr = Console.OpenStandardError();
        await using var stderrWriter = new StreamWriter(stderr) { AutoFlush = true };

        try
        {
            var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
                options
                    .WithInput(Console.OpenStandardInput())
                    .WithOutput(Console.OpenStandardOutput())
                    .ConfigureLogging(logging =>
                    {
                        logging.SetMinimumLevel(LogLevel.Warning);
                        logging.AddLanguageProtocolLogging();
                    })
                    .WithServices(services =>
                    {
                        services.AddSingleton<DocumentStore>();
                        services.AddSingleton<SqlAnalysisService>();
                        services.AddSingleton<DiagnosticPublisher>();
                    })
                    .WithHandler<TextDocumentHandler>()
                    .WithHandler<AnalyzeCommandHandler>()
            ).ConfigureAwait(false);

            await server.WaitForExit.ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            await stderrWriter.WriteLineAsync($"SQLGuardian Language Server failed: {ex}").ConfigureAwait(false);
            return 1;
        }
    }
}
