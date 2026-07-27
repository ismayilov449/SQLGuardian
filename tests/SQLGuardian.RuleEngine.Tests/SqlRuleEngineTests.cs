using SQLGuardian.Abstractions;
using SQLGuardian.Domain;
using SQLGuardian.RuleEngine;
using SQLGuardian.ScriptDom;

namespace SQLGuardian.RuleEngine.Tests;

public class SqlRuleEngineTests
{
    [Fact]
    public void Analyze_WithNoRules_ReturnsEmptyIssues()
    {
        var engine = new SqlRuleEngine(rules: []);
        var report = engine.Analyze("SELECT 1;", "sample.sql");

        Assert.Empty(report.Issues);
        Assert.Empty(report.ParseErrors);
        Assert.Equal("sample.sql", report.Target);
    }

    [Fact]
    public void Analyze_DisabledRule_IsSkipped()
    {
        var config = new RuleConfiguration();
        config.Disable("SQLG0000");

        var engine = new SqlRuleEngine(
            rules: [new StubRule()],
            configuration: config);

        var report = engine.Analyze("SELECT 1;", "sample.sql");

        Assert.Empty(report.Issues);
    }

    [Fact]
    public void Analyze_SeverityOverride_IsApplied()
    {
        var config = new RuleConfiguration();
        config.Set("SQLG0000", Severity.Critical);

        var engine = new SqlRuleEngine(
            rules: [new StubRule()],
            configuration: config);

        var report = engine.Analyze("SELECT 1;", "sample.sql");

        Assert.Single(report.Issues);
        Assert.Equal(Severity.Critical, report.Issues[0].Severity);
    }

    private sealed class StubRule : ISqlRule
    {
        public string RuleId => "SQLG0000";
        public string Title => "Stub";
        public string Description => "Task 1 placeholder rule for engine wiring tests.";
        public Severity Severity => Severity.Low;
        public RuleCategory Category => RuleCategory.BestPractices;

        public RuleResult Analyze(SqlAnalysisContext context) =>
            RuleResult.FromIssues(new Issue
            {
                RuleId = RuleId,
                Title = Title,
                Message = "Stub finding",
                Severity = Severity,
                Category = Category,
                Location = SourceLocation.Unknown,
                FilePath = context.FilePath
            });
    }
}

public class ScriptDomSqlParserTests
{
    [Fact]
    public void Parse_ValidSql_Succeeds()
    {
        var parser = new ScriptDomSqlParser();
        var result = parser.Parse("SELECT 1 AS Value;");

        Assert.True(result.Success);
        Assert.NotNull(result.SyntaxTree);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_InvalidSql_ReportsErrors()
    {
        var parser = new ScriptDomSqlParser();
        var result = parser.Parse("SELECT FROM;");

        Assert.NotEmpty(result.Errors);
    }
}

public class ContractSmokeTests
{
    [Fact]
    public void Severity_And_Category_Enums_AreDefined()
    {
        Assert.Equal(5, Enum.GetValues<Severity>().Length);
        Assert.True(Enum.GetValues<RuleCategory>().Length >= 8);
    }

    [Fact]
    public void RuleConfiguration_Defaults_EnableAllRules()
    {
        var config = new RuleConfiguration();
        Assert.True(config.IsEnabled("SQLG0001"));
        Assert.Equal(Severity.Medium, config.ResolveSeverity("SQLG0001", Severity.Medium));
    }
}
