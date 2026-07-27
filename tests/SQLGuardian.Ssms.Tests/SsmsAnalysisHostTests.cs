using System.IO;
using SQLGuardian.Ssms.Services;

namespace SQLGuardian.Ssms.Tests;

public class SsmsAnalysisHostTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(Path.Combine(dir.FullName, "SQLGuardian.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void ExpandSqlFiles_FromFolder_FindsSamples()
    {
        var samples = Path.Combine(RepoRoot(), "samples", "visitors");
        var files = SsmsAnalysisHost.ExpandSqlFiles([samples]);

        Assert.True(files.Count >= 3);
        Assert.All(files, f => Assert.EndsWith(".sql", f, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzePaths_ReturnsKnownRules()
    {
        var file = Path.Combine(RepoRoot(), "samples", "visitors", "columns_and_predicates.sql");
        var host = new SsmsAnalysisHost();
        var run = host.AnalyzePaths([file]);

        Assert.Equal(1, run.FileCount);
        Assert.True(run.IssueCount >= 2);
        Assert.Contains(run.AllIssues, i => i.RuleId == "SQLG0001");
        Assert.Contains(run.AllIssues, i => i.RuleId == "SQLG0005");
    }

    [Fact]
    public void IssueRow_FromRun_MapsFields()
    {
        var file = Path.Combine(RepoRoot(), "samples", "visitors", "tables_and_joins.sql");
        var run = new SsmsAnalysisHost().AnalyzePaths([file]);
        var rows = IssueRow.FromRun(run);

        Assert.Contains(rows, r => r.RuleId == "SQLG0006");
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Message)));
    }

    [Fact]
    public void FindConfigNear_FindsSampleConfig()
    {
        var temp = Path.Combine(Path.GetTempPath(), "sqlguardian-ssms-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            System.IO.File.WriteAllText(Path.Combine(temp, "sqlguardian.json"), """{ "SQLG0009": "Disabled" }""");
            var sql = Path.Combine(temp, "a.sql");
            System.IO.File.WriteAllText(sql, "SELECT 1;");
            var found = SsmsAnalysisHost.FindConfigNear(sql);
            Assert.Equal(Path.Combine(temp, "sqlguardian.json"), found);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
