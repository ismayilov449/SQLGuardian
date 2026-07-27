using System.Text.Json;
using SQLGuardian.Abstractions;
using SQLGuardian.Domain;

namespace SQLGuardian.RuleEngine;

/// <summary>
/// Loads EditorConfig-style rule severity overrides from JSON.
/// Keys may be rule IDs (<c>SQLG0001</c>) or rule type names (<c>SelectStarRule</c>).
/// </summary>
public static class RuleConfigurationLoader
{
    public static RuleConfiguration LoadJson(string json, IEnumerable<ISqlRule>? rules = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("rules", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            root = nested;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Rule configuration JSON must be an object.");
        }

        var lookup = BuildKeyLookup(rules);
        var configuration = new RuleConfiguration();

        foreach (var property in root.EnumerateObject())
        {
            var value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.ToString();

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var ruleId = ResolveRuleId(property.Name, lookup);
            Apply(configuration, ruleId, value);
        }

        return configuration;
    }

    public static RuleConfiguration LoadFile(string path, IEnumerable<ISqlRule>? rules = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path);
        return LoadJson(json, rules);
    }

    public static bool TryLoadFile(string path, out RuleConfiguration configuration, IEnumerable<ISqlRule>? rules = null)
    {
        configuration = new RuleConfiguration();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        configuration = LoadFile(path, rules);
        return true;
    }

    private static Dictionary<string, string> BuildKeyLookup(IEnumerable<ISqlRule>? rules)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rules is null)
        {
            return lookup;
        }

        foreach (var rule in rules)
        {
            lookup[rule.RuleId] = rule.RuleId;
            lookup[rule.GetType().Name] = rule.RuleId;
            var titleKey = rule.Title.Replace(" ", string.Empty, StringComparison.Ordinal);
            lookup[titleKey] = rule.RuleId;
        }

        return lookup;
    }

    private static string ResolveRuleId(string key, IReadOnlyDictionary<string, string> lookup) =>
        lookup.TryGetValue(key, out var ruleId) ? ruleId : key;

    private static void Apply(RuleConfiguration configuration, string ruleId, string rawValue)
    {
        var value = rawValue.Trim();
        if (value.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Off", StringComparison.OrdinalIgnoreCase)
            || value.Equals("None", StringComparison.OrdinalIgnoreCase)
            || value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            configuration.Disable(ruleId);
            return;
        }

        configuration.Set(ruleId, ParseSeverity(value));
    }

    private static Severity ParseSeverity(string value) => value.ToLowerInvariant() switch
    {
        "critical" => Severity.Critical,
        "high" or "error" => Severity.High,
        "medium" or "warning" or "warn" => Severity.Medium,
        "low" => Severity.Low,
        "info" or "information" or "hint" => Severity.Info,
        _ => throw new InvalidOperationException(
            $"Unknown severity '{value}'. Use Critical, High, Medium, Low, Info, Error, Warning, or Disabled.")
    };
}
