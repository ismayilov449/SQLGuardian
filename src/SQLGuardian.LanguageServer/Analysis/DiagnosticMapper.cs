using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SQLGuardian.Abstractions;
using SQLGuardian.Domain;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SQLGuardian.LanguageServer.Analysis;

/// <summary>
/// Maps RuleEngine issues to LSP diagnostics (shared with tests).
/// </summary>
public static class DiagnosticMapper
{
    public static IReadOnlyList<Diagnostic> Map(AnalysisReport report) =>
        MapCore(report).ToList();

    private static IEnumerable<Diagnostic> MapCore(AnalysisReport report)
    {
        foreach (var error in report.ParseErrors)
        {
            yield return new Diagnostic
            {
                Severity = DiagnosticSeverity.Error,
                Message = error,
                Source = "SQLGuardian",
                Code = "SQLG-PARSE",
                Range = new LspRange(new Position(0, 0), new Position(0, 1))
            };
        }

        foreach (var issue in report.Issues)
        {
            yield return new Diagnostic
            {
                Severity = ToLspSeverity(issue.Severity),
                Message = FormatMessage(issue),
                Source = "SQLGuardian",
                Code = issue.RuleId,
                Range = ToRange(issue.Location)
            };
        }
    }

    public static string FormatMessage(Issue issue)
    {
        if (string.IsNullOrWhiteSpace(issue.Suggestion))
        {
            return $"[{issue.RuleId}] {issue.Message}";
        }

        return $"[{issue.RuleId}] {issue.Message} Suggestion: {issue.Suggestion}";
    }

    public static DiagnosticSeverity ToLspSeverity(Severity severity) => severity switch
    {
        Severity.Critical or Severity.High => DiagnosticSeverity.Error,
        Severity.Medium => DiagnosticSeverity.Warning,
        Severity.Low => DiagnosticSeverity.Information,
        _ => DiagnosticSeverity.Hint
    };

    public static LspRange ToRange(SourceLocation location)
    {
        // LSP is 0-based; SQLGuardian SourceLocation is 1-based.
        var startLine = Math.Max(0, location.StartLine - 1);
        var startCol = Math.Max(0, location.StartColumn - 1);
        var endLine = Math.Max(startLine, location.EndLine - 1);
        var endCol = Math.Max(startCol + 1, location.EndColumn - 1);
        return new LspRange(new Position(startLine, startCol), new Position(endLine, endCol));
    }
}
