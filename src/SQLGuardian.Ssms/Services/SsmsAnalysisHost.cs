using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.Domain;
using SQLGuardian.RuleEngine;
using SQLGuardian.Schema;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using IODirectory = System.IO.Directory;

namespace SQLGuardian.Ssms.Services;

/// <summary>
/// Shared analysis host for the SSMS companion (same engine as CLI).
/// </summary>
public sealed class SsmsAnalysisHost
{
    private readonly SchemaSnapshotCache _schemaCache = new();

    public AnalysisRun AnalyzePaths(
        IEnumerable<string> paths,
        string? configPath = null,
        string? connectionString = null)
    {
        var files = ExpandSqlFiles(paths).ToList();
        var rules = RuleCatalog.CreateDefault();
        var configuration = LoadConfiguration(configPath, rules);
        var engine = new SqlRuleEngine(rules, configuration: configuration);
        var schema = LoadSchemaOrNull(connectionString, SchemaLoadProfile.Full);

        var reports = new List<AnalysisReport>(files.Count);
        foreach (var file in files)
        {
            var text = IOFile.ReadAllText(file);
            reports.Add(engine.Analyze(text, file, schema));
        }

        return new AnalysisRun
        {
            Reports = reports,
            ToolName = "SQLGuardian for SSMS",
            ToolVersion = "0.6.1"
        };
    }

    public AnalysisRun AnalyzeCatalog(string connectionString, string? configPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var rules = RuleCatalog.CreateDefault();
        var configuration = LoadConfiguration(configPath, rules);
        var engine = new SqlRuleEngine(rules, configuration: configuration);
        var schema = LoadSchemaOrNull(connectionString, SchemaLoadProfile.CatalogScan)
                     ?? throw new InvalidOperationException("Failed to load catalog schema.");

        return new AnalysisRun
        {
            Reports = [engine.AnalyzeCatalog(schema)],
            ToolName = "SQLGuardian for SSMS",
            ToolVersion = "0.6.1"
        };
    }

    /// <summary>Clears the in-process schema cache (force next scan to hit SQL Server).</summary>
    public void InvalidateSchemaCache() => _schemaCache.Invalidate();

    private SchemaSnapshot? LoadSchemaOrNull(string? connectionString, SchemaLoadProfile profile)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        return _schemaCache.GetOrAdd(
            connectionString,
            profile,
            p => new SqlServerSchemaProvider(connectionString)
                .LoadAsync(new SchemaLoadOptions { Profile = p })
                .GetAwaiter()
                .GetResult());
    }

    public static IReadOnlyList<string> ExpandSqlFiles(IEnumerable<string> paths)
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var full = IOPath.GetFullPath(path);
            if (IOFile.Exists(full))
            {
                if (full.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(full);
                }

                continue;
            }

            if (IODirectory.Exists(full))
            {
                foreach (var file in IODirectory.EnumerateFiles(full, "*.sql", System.IO.SearchOption.AllDirectories))
                {
                    files.Add(IOPath.GetFullPath(file));
                }
            }
        }

        return files.ToList();
    }

    public static string? FindConfigNear(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var start = IOFile.Exists(path)
                ? IOPath.GetDirectoryName(IOPath.GetFullPath(path))
                : (IODirectory.Exists(path) ? IOPath.GetFullPath(path) : null);

            for (var dir = start; dir is not null; dir = IODirectory.GetParent(dir)?.FullName)
            {
                var candidate = IOPath.Combine(dir, "sqlguardian.json");
                if (IOFile.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static RuleConfiguration LoadConfiguration(string? configPath, IReadOnlyList<ISqlRule> rules)
    {
        if (!string.IsNullOrWhiteSpace(configPath)
            && RuleConfigurationLoader.TryLoadFile(configPath, out var loaded, rules))
        {
            return loaded;
        }

        return new RuleConfiguration();
    }
}

public sealed class IssueRow
{
    public required string Severity { get; init; }
    public required string RuleId { get; init; }
    public required string File { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required string Message { get; init; }
    public string? Suggestion { get; init; }
    public string? SuggestedSql { get; init; }
    public required string FullPath { get; init; }

    public static IReadOnlyList<IssueRow> FromRun(AnalysisRun run) =>
        run.Reports
            .SelectMany(r => r.Issues.Select(i => new IssueRow
            {
                Severity = i.Severity.ToString(),
                RuleId = i.RuleId,
                File = IOPath.GetFileName(r.Target),
                Line = i.Location.StartLine,
                Column = i.Location.StartColumn,
                Message = i.Message,
                Suggestion = i.Suggestion,
                SuggestedSql = i.SuggestedSql,
                FullPath = r.Target
            }))
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Line)
            .ToList();
}
