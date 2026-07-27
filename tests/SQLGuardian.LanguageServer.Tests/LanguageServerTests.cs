using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SQLGuardian.Abstractions;
using SQLGuardian.Domain;
using SQLGuardian.LanguageServer.Analysis;
using SQLGuardian.RuleEngine;

namespace SQLGuardian.LanguageServer.Tests;

public class DiagnosticMapperTests
{
    [Fact]
    public void Map_UsesSameRuleIds_AsEngine()
    {
        var engine = new SqlRuleEngine(RuleCatalog.CreateDefault());
        var report = engine.Analyze("SELECT * FROM dbo.Users;", "demo.sql");

        var diagnostics = DiagnosticMapper.Map(report);

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Message.StartsWith("[SQLG0001]", StringComparison.Ordinal));
        Assert.All(diagnostics, d => Assert.Equal("SQLGuardian", d.Source));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ToRange_ConvertsOneBasedToZeroBased()
    {
        var range = DiagnosticMapper.ToRange(new SourceLocation(3, 8, 3, 9));

        Assert.Equal(2, range.Start.Line);
        Assert.Equal(7, range.Start.Character);
        Assert.Equal(2, range.End.Line);
        Assert.Equal(8, range.End.Character);
    }

    [Fact]
    public void ToLspSeverity_MapsHighToError()
    {
        Assert.Equal(DiagnosticSeverity.Error, DiagnosticMapper.ToLspSeverity(Severity.High));
        Assert.Equal(DiagnosticSeverity.Warning, DiagnosticMapper.ToLspSeverity(Severity.Medium));
        Assert.Equal(DiagnosticSeverity.Information, DiagnosticMapper.ToLspSeverity(Severity.Low));
        Assert.Equal(DiagnosticSeverity.Hint, DiagnosticMapper.ToLspSeverity(Severity.Info));
    }
}

public class SqlAnalysisServiceTests
{
    [Fact]
    public void Analyze_MatchesCliEngine_ForSelectStar()
    {
        var service = new SqlAnalysisService();
        var report = service.Analyze("SELECT * FROM dbo.Users WITH (NOLOCK);", "x.sql");

        Assert.Contains(report.Issues, i => i.RuleId == "SQLG0001");
        Assert.Contains(report.Issues, i => i.RuleId == "SQLG0003");
    }
}

public class DocumentStoreTests
{
    [Fact]
    public void Upsert_And_Remove_Work()
    {
        var store = new DocumentStore();
        store.Upsert("file:///a.sql", "SELECT 1;", 1);
        Assert.True(store.TryGet("file:///a.sql", out var state));
        Assert.Equal("SELECT 1;", state.Text);
        Assert.True(store.Remove("file:///a.sql"));
        Assert.False(store.TryGet("file:///a.sql", out _));
    }
}
