using SQLGuardian.Abstractions;

namespace SQLGuardian.RuleEngine;

/// <summary>
/// Discovers and instantiates built-in <see cref="ISqlRule"/> implementations.
/// </summary>
public static class RuleCatalog
{
    public static IReadOnlyList<ISqlRule> CreateDefault()
    {
        var rules = typeof(RuleCatalog).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(ISqlRule).IsAssignableFrom(t)
                        && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (ISqlRule)Activator.CreateInstance(t)!)
            .OrderBy(r => r.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return rules;
    }
}
