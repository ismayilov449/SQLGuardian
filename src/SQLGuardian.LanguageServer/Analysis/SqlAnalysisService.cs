using SQLGuardian.Domain;
using SQLGuardian.RuleEngine;

namespace SQLGuardian.LanguageServer.Analysis;

/// <summary>
/// Runs the same <see cref="SqlRuleEngine"/> as the CLI — no second analyzer.
/// </summary>
public sealed class SqlAnalysisService
{
    private readonly object _gate = new();
    private SqlRuleEngine _engine;
    private string? _configPath;

    public SqlAnalysisService()
    {
        _engine = CreateEngine(null);
    }

    public AnalysisReport Analyze(string sourceText, string filePath)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        lock (_gate)
        {
            return _engine.Analyze(sourceText, filePath);
        }
    }

    public void ReloadConfiguration(string? configPath)
    {
        lock (_gate)
        {
            if (string.Equals(_configPath, configPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _configPath = configPath;
            _engine = CreateEngine(configPath);
        }
    }

    public void EnsureConfigurationNear(string filePath)
    {
        var discovered = FindConfigPath(filePath);
        if (discovered is not null)
        {
            ReloadConfiguration(discovered);
        }
    }

    public string? ConfigPath
    {
        get
        {
            lock (_gate)
            {
                return _configPath;
            }
        }
    }

    private static SqlRuleEngine CreateEngine(string? configPath)
    {
        var rules = RuleCatalog.CreateDefault();
        RuleConfiguration configuration = new();
        if (!string.IsNullOrWhiteSpace(configPath)
            && RuleConfigurationLoader.TryLoadFile(configPath, out var loaded, rules))
        {
            configuration = loaded;
        }

        return new SqlRuleEngine(rules, configuration: configuration);
    }

    private static string? FindConfigPath(string filePath)
    {
        try
        {
            var start = File.Exists(filePath)
                ? Path.GetDirectoryName(Path.GetFullPath(filePath))
                : Path.GetDirectoryName(Path.GetFullPath(filePath));

            for (var dir = start; dir is not null; dir = Directory.GetParent(dir)?.FullName)
            {
                var candidate = Path.Combine(dir, "sqlguardian.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // ignore invalid paths from untitled buffers
        }

        return null;
    }
}
