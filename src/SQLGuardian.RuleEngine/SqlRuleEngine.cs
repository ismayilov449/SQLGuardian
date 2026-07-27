using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.Domain;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine;

/// <summary>
/// Runs registered <see cref="ISqlRule"/> instances against parsed SQL.
/// </summary>
public sealed class SqlRuleEngine
{
    private readonly ISqlParser _parser;
    private readonly IReadOnlyList<ISqlRule> _rules;
    private readonly RuleConfiguration _configuration;

    public SqlRuleEngine(
        IEnumerable<ISqlRule> rules,
        ISqlParser? parser = null,
        RuleConfiguration? configuration = null)
    {
        _rules = rules?.ToList() ?? throw new ArgumentNullException(nameof(rules));
        _parser = parser ?? new ScriptDomSqlParser();
        _configuration = configuration ?? new RuleConfiguration();
    }

    public IReadOnlyList<ISqlRule> Rules => _rules;

    public AnalysisReport Analyze(
        string sourceText,
        string filePath = "<memory>",
        SchemaSnapshot? schema = null)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        var parseResult = _parser.Parse(sourceText, filePath);
        var context = parseResult.ToAnalysisContext(schema);
        return RunRules(context, filePath, parseResult.Errors);
    }

    /// <summary>
    /// Runs schema-aware rules against a catalog snapshot without a SQL script.
    /// </summary>
    public AnalysisReport AnalyzeCatalog(SchemaSnapshot schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var context = new SqlAnalysisContext
        {
            SourceText = string.Empty,
            FilePath = SchemaAnalysisTargets.Catalog,
            SyntaxTree = null,
            Schema = schema
        };

        return RunRules(context, SchemaAnalysisTargets.Catalog, []);
    }

    private AnalysisReport RunRules(
        SqlAnalysisContext context,
        string target,
        IReadOnlyList<string> parseErrors)
    {
        var issues = new List<Issue>();

        foreach (var rule in _rules)
        {
            if (!_configuration.IsEnabled(rule.RuleId))
            {
                continue;
            }

            var result = rule.Analyze(context);
            foreach (var issue in result.Issues)
            {
                var severity = _configuration.ResolveSeverity(rule.RuleId, issue.Severity);
                issues.Add(CloneWithSeverity(issue, severity));
            }
        }

        return new AnalysisReport
        {
            Target = target,
            Issues = issues,
            ParseErrors = parseErrors,
            RecommendedSql = RecommendationComposer.Compose(issues, Path.GetFileName(target))
        };
    }

    private static Issue CloneWithSeverity(Issue issue, Severity severity) => new()
    {
        RuleId = issue.RuleId,
        Title = issue.Title,
        Message = issue.Message,
        Severity = severity,
        Category = issue.Category,
        Location = issue.Location,
        Suggestion = issue.Suggestion,
        SuggestedSql = issue.SuggestedSql,
        FilePath = issue.FilePath,
        Tags = issue.Tags
    };
}
