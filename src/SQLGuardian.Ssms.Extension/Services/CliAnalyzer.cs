using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SQLGuardian.Ssms.Extension;

public static class CliAnalyzer
{
    public static CliAnalyzeResult AnalyzeFile(
        string sqlFilePath,
        string? connectionString,
        string? cliPathOverride)
    {
        try
        {
            var cli = ResolveCliPath(cliPathOverride);
            if (cli is null)
            {
                return CliAnalyzeResult.Fail(
                    "sqlguardian CLI not found. Build the extension (bundles tools\\cli) or set Tools → Options → SQLGuardian → CLI path override.");
            }

            var args = new StringBuilder();
            args.Append("analyze ");
            args.Append('"').Append(sqlFilePath).Append('"');
            args.Append(" --format json --quiet --fail-on never");

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                args.Append(" --connection \"").Append(connectionString!.Replace("\"", "\\\"")).Append('"');
            }

            return RunAnalyzeProcess(cli, args.ToString());
        }
        catch (Exception ex)
        {
            return CliAnalyzeResult.Fail(ex.Message);
        }
    }

    public static CliPrecheckResult PrecheckFile(
        string sqlFilePath,
        string? connectionString,
        long rowThreshold,
        string? cliPathOverride)
    {
        try
        {
            var cli = ResolveCliPath(cliPathOverride);
            if (cli is null)
            {
                return CliPrecheckResult.Fail(
                    "sqlguardian CLI not found. Build the extension (bundles tools\\cli) or set Tools → Options → SQLGuardian → CLI path override.");
            }

            var args = new StringBuilder();
            args.Append("precheck ");
            args.Append('"').Append(sqlFilePath).Append('"');
            args.Append(" --quiet");
            args.Append(" --row-threshold ").Append(rowThreshold);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                args.Append(" --connection \"").Append(connectionString!.Replace("\"", "\\\"")).Append('"');
            }

            var start = CreateProcessStart(cli, args.ToString());
            using var process = Process.Start(start);
            if (process is null)
            {
                return CliPrecheckResult.Fail("Failed to start sqlguardian CLI.");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);

            if (process.ExitCode != 0)
            {
                return CliPrecheckResult.Fail(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                return CliPrecheckResult.OkEmpty();
            }

            return CliPrecheckResult.Ok(ParsePrecheckResult(stdout));
        }
        catch (Exception ex)
        {
            return CliPrecheckResult.Fail(ex.Message);
        }
    }

    private static CliAnalyzeResult RunAnalyzeProcess(string cli, string arguments)
    {
        var start = CreateProcessStart(cli, arguments);
        using var process = Process.Start(start);
        if (process is null)
        {
            return CliAnalyzeResult.Fail("Failed to start sqlguardian CLI.");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);

        if (process.ExitCode > 1)
        {
            return CliAnalyzeResult.Fail(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return CliAnalyzeResult.Fail(
                string.IsNullOrWhiteSpace(stderr)
                    ? "CLI returned no JSON output."
                    : stderr);
        }

        return CliAnalyzeResult.Ok(ParseFindings(stdout));
    }

    private static ProcessStartInfo CreateProcessStart(string cli, string arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = cli,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(cli) ?? Environment.CurrentDirectory
        };

        if (cli.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            start.FileName = "dotnet";
            start.Arguments = '"' + cli + "\" " + arguments;
        }

        return start;
    }

    private static string? ResolveCliPath(string? cliPathOverride)
    {
        if (!string.IsNullOrWhiteSpace(cliPathOverride) && File.Exists(cliPathOverride))
        {
            return cliPathOverride;
        }

        var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
        var candidates = new[]
        {
            Path.Combine(baseDir, "tools", "cli", "sqlguardian.exe"),
            Path.Combine(baseDir, "tools", "cli", "SQLGuardian.Cli.exe"),
            Path.Combine(baseDir, "tools", "cli", "SQLGuardian.Cli.dll"),
            Path.Combine(baseDir, "sqlguardian.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IReadOnlyList<GuardianFinding> ParseFindings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var list = new List<GuardianFinding>();

        if (!root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var file in files.EnumerateArray())
        {
            var path = file.TryGetProperty("path", out var p) ? p.GetString() : null;
            if (!file.TryGetProperty("issues", out var issues) || issues.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var issue in issues.EnumerateArray())
            {
                var location = issue.TryGetProperty("location", out var loc) ? loc : default;
                list.Add(new GuardianFinding
                {
                    RuleId = issue.TryGetProperty("ruleId", out var rid) ? rid.GetString() ?? "" : "",
                    Title = issue.TryGetProperty("title", out var title) ? title.GetString() : null,
                    Message = issue.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "",
                    Severity = issue.TryGetProperty("severity", out var sev) ? sev.GetString() : "Medium",
                    Suggestion = issue.TryGetProperty("suggestion", out var sug) ? sug.GetString() : null,
                    SuggestedSql = issue.TryGetProperty("suggestedSql", out var sql) ? sql.GetString() : null,
                    FilePath = path,
                    StartLine = location.ValueKind == JsonValueKind.Object && location.TryGetProperty("startLine", out var sl)
                        ? sl.GetInt32()
                        : 1,
                    StartColumn = location.ValueKind == JsonValueKind.Object && location.TryGetProperty("startColumn", out var sc)
                        ? sc.GetInt32()
                        : 1
                });
            }
        }

        return list;
    }

    private static ExecuteGuardSnapshot ParsePrecheckResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var missingWhere = new List<MissingWhereWarningDto>();
        if (root.TryGetProperty("missingWhere", out var mw) && mw.ValueKind == JsonValueKind.Array)
        {
            foreach (var w in mw.EnumerateArray())
            {
                missingWhere.Add(new MissingWhereWarningDto
                {
                    StatementKind = w.TryGetProperty("statementKind", out var sk) ? sk.GetString() ?? "" : "",
                    Target = w.TryGetProperty("target", out var t) ? t.GetString() : null,
                    Message = w.TryGetProperty("message", out var m) ? m.GetString() ?? "" : ""
                });
            }
        }

        var largeReads = ParseWarningArray(
            root.TryGetProperty("largeReads", out var lr) ? lr : default,
            fallback: root.TryGetProperty("warnings", out var legacy) ? legacy : default);

        var largeJoins = new List<LargeJoinWarningDto>();
        if (root.TryGetProperty("largeJoins", out var lj) && lj.ValueKind == JsonValueKind.Array)
        {
            foreach (var w in lj.EnumerateArray())
            {
                largeJoins.Add(new LargeJoinWarningDto
                {
                    Schema = w.TryGetProperty("schema", out var s) ? s.GetString() ?? "" : "",
                    Table = w.TryGetProperty("table", out var t) ? t.GetString() ?? "" : "",
                    Alias = w.TryGetProperty("alias", out var a) ? a.GetString() : null,
                    ApproximateRowCount = w.TryGetProperty("approximateRowCount", out var rc) ? rc.GetInt64() : 0,
                    Message = w.TryGetProperty("message", out var m) ? m.GetString() ?? "" : ""
                });
            }
        }

        return new ExecuteGuardSnapshot
        {
            MissingWhere = missingWhere,
            LargeReads = largeReads,
            LargeJoins = largeJoins,
            SuggestedNolockSql = root.TryGetProperty("suggestedNolockSql", out var sn)
                ? sn.GetString()
                : null
        };
    }

    private static IReadOnlyList<LargeTableWarning> ParseWarningArray(JsonElement primary, JsonElement fallback)
    {
        var source = primary.ValueKind == JsonValueKind.Array
            ? primary
            : fallback.ValueKind == JsonValueKind.Array
                ? fallback
                : default;

        var list = new List<LargeTableWarning>();
        if (source.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var w in source.EnumerateArray())
        {
            list.Add(new LargeTableWarning
            {
                Schema = w.TryGetProperty("schema", out var s) ? s.GetString() ?? "" : "",
                Table = w.TryGetProperty("table", out var t) ? t.GetString() ?? "" : "",
                ApproximateRowCount = w.TryGetProperty("approximateRowCount", out var rc) ? rc.GetInt64() : 0,
                Kind = w.TryGetProperty("kind", out var k) ? k.GetString() : null,
                Message = w.TryGetProperty("message", out var m) ? m.GetString() ?? "" : ""
            });
        }

        return list;
    }
}

public sealed class GuardianFinding
{
    public string RuleId { get; set; } = "";
    public string? Title { get; set; }
    public string Message { get; set; } = "";
    public string? Severity { get; set; }
    public string? Suggestion { get; set; }
    public string? SuggestedSql { get; set; }
    public string? FilePath { get; set; }
    public int StartLine { get; set; } = 1;
    public int StartColumn { get; set; } = 1;
}

public sealed class LargeTableWarning
{
    public string Schema { get; set; } = "";
    public string Table { get; set; } = "";
    public long ApproximateRowCount { get; set; }
    public string? Kind { get; set; }
    public string Message { get; set; } = "";
}

public sealed class MissingWhereWarningDto
{
    public string StatementKind { get; set; } = "";
    public string? Target { get; set; }
    public string Message { get; set; } = "";
}

public sealed class LargeJoinWarningDto
{
    public string Schema { get; set; } = "";
    public string Table { get; set; } = "";
    public string? Alias { get; set; }
    public long ApproximateRowCount { get; set; }
    public string Message { get; set; } = "";
}

public sealed class ExecuteGuardSnapshot
{
    public IReadOnlyList<MissingWhereWarningDto> MissingWhere { get; set; } = Array.Empty<MissingWhereWarningDto>();
    public IReadOnlyList<LargeTableWarning> LargeReads { get; set; } = Array.Empty<LargeTableWarning>();
    public IReadOnlyList<LargeJoinWarningDto> LargeJoins { get; set; } = Array.Empty<LargeJoinWarningDto>();
    public string? SuggestedNolockSql { get; set; }

    public int WarningCount => MissingWhere.Count + LargeReads.Count + LargeJoins.Count;
}

public sealed class CliAnalyzeResult
{
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IReadOnlyList<GuardianFinding> Findings { get; private set; } = Array.Empty<GuardianFinding>();

    public static CliAnalyzeResult Ok(IReadOnlyList<GuardianFinding> findings) => new()
    {
        Success = true,
        Findings = findings
    };

    public static CliAnalyzeResult Fail(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}

public sealed class CliPrecheckResult
{
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
    public ExecuteGuardSnapshot Snapshot { get; private set; } = new();

    /// <summary>Backward-compatible view of large-read warnings.</summary>
    public IReadOnlyList<LargeTableWarning> Warnings => Snapshot.LargeReads;

    public static CliPrecheckResult Ok(ExecuteGuardSnapshot snapshot) => new()
    {
        Success = true,
        Snapshot = snapshot
    };

    public static CliPrecheckResult OkEmpty() => Ok(new ExecuteGuardSnapshot());

    public static CliPrecheckResult Fail(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}
