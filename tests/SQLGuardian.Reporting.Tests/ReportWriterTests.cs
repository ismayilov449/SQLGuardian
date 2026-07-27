using System.Text.Json;
using SQLGuardian.Abstractions;
using SQLGuardian.Domain;
using SQLGuardian.Reporting;

namespace SQLGuardian.Reporting.Tests;

public class ReportWriterTests
{
    private static AnalysisRun CreateSampleRun()
    {
        return new AnalysisRun
        {
            ToolVersion = "0.4.0",
            Reports =
            [
                new AnalysisReport
                {
                    Target = Path.Combine("samples", "demo.sql"),
                    ParseErrors = [],
                    Issues =
                    [
                        new Issue
                        {
                            RuleId = "SQLG0001",
                            Title = "Avoid SELECT *",
                            Message = "SELECT * expands to all columns.",
                            Severity = Severity.Medium,
                            Category = RuleCategory.Performance,
                            Location = new SourceLocation(3, 8, 3, 9),
                            Suggestion = "List only the columns you need.",
                            FilePath = Path.Combine("samples", "demo.sql"),
                            Tags = ["select", "wildcard"]
                        },
                        new Issue
                        {
                            RuleId = "SQLG0003",
                            Title = "Avoid NOLOCK table hint",
                            Message = "NOLOCK can return dirty reads.",
                            Severity = Severity.High,
                            Category = RuleCategory.Concurrency,
                            Location = new SourceLocation(5, 1, 5, 10),
                            FilePath = Path.Combine("samples", "demo.sql"),
                            Tags = ["nolock"]
                        }
                    ]
                }
            ]
        };
    }

    [Fact]
    public void Json_ContainsIssuesAndCamelCase()
    {
        var json = new JsonReportWriter().Write(CreateSampleRun(), new ReportWriteOptions
        {
            BaseDirectory = Directory.GetCurrentDirectory()
        });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("SQLGuardian", root.GetProperty("tool").GetString());
        Assert.Equal(2, root.GetProperty("issueCount").GetInt32());
        Assert.Equal("SQLG0001", root.GetProperty("files")[0].GetProperty("issues")[0].GetProperty("ruleId").GetString());
        Assert.Equal("medium", root.GetProperty("files")[0].GetProperty("issues")[0].GetProperty("severity").GetString());
    }

    [Fact]
    public void Sarif_IsVersion210_WithResultsAndRules()
    {
        var sarif = new SarifReportWriter().Write(CreateSampleRun(), new ReportWriteOptions
        {
            BaseDirectory = Directory.GetCurrentDirectory()
        });

        using var doc = JsonDocument.Parse(sarif);
        var root = doc.RootElement;
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        Assert.Contains("sarif-2.1.0", root.GetProperty("$schema").GetString(), StringComparison.OrdinalIgnoreCase);

        var run = root.GetProperty("runs")[0];
        Assert.Equal(2, run.GetProperty("results").GetArrayLength());
        Assert.Equal(2, run.GetProperty("tool").GetProperty("driver").GetProperty("rules").GetArrayLength());

        var levels = run.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("level").GetString()).ToArray();
        Assert.Contains("warning", levels);
        Assert.Contains("error", levels);
    }

    [Fact]
    public void Markdown_ContainsTableAndSummary()
    {
        var md = new MarkdownReportWriter().Write(CreateSampleRun());

        Assert.Contains("# SQLGuardian Report", md, StringComparison.Ordinal);
        Assert.Contains("| Severity | Rule | Line | Message |", md, StringComparison.Ordinal);
        Assert.Contains("SQLG0001", md, StringComparison.Ordinal);
        Assert.Contains("SQLG0003", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ListsIssuesPerFile()
    {
        var text = new TextReportWriter().Write(CreateSampleRun());

        Assert.Contains("2 issue(s)", text, StringComparison.Ordinal);
        Assert.Contains("SQLG0001", text, StringComparison.Ordinal);
        Assert.Contains("Analyzed 1 file(s)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalysisRun_FailureThreshold_RespectsSeverity()
    {
        var run = CreateSampleRun();
        Assert.True(run.MeetsFailureThreshold(Severity.High));
        Assert.True(run.MeetsFailureThreshold(Severity.Medium));
        Assert.False(run.MeetsFailureThreshold(Severity.Critical));
    }
}

public class FailureThresholdParserTests
{
    [Theory]
    [InlineData("high", Severity.High)]
    [InlineData("warning", Severity.Medium)]
    [InlineData("never", null)]
    public void Parse_KnownValues(string input, Severity? expected) =>
        Assert.Equal(expected, FailureThresholdParser.Parse(input));

    [Fact]
    public void Parse_Unknown_Throws() =>
        Assert.Throws<ArgumentException>(() => FailureThresholdParser.Parse("nope"));
}

public class ReportWriterFactoryTests
{
    [Theory]
    [InlineData("json", ReportFormat.Json)]
    [InlineData("sarif", ReportFormat.Sarif)]
    [InlineData("md", ReportFormat.Markdown)]
    [InlineData("text", ReportFormat.Text)]
    public void TryParseFormat_Succeeds(string input, ReportFormat expected)
    {
        Assert.True(ReportWriterFactory.TryParseFormat(input, out var format));
        Assert.Equal(expected, format);
    }
}
