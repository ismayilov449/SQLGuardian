using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.ScriptDom.Tests;

internal static class SampleSql
{
    public static string ReadVisitorSample(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "samples", "visitors", fileName);
        Assert.True(File.Exists(path), $"Missing sample fixture: {path}");
        return File.ReadAllText(path);
    }

    public static TSqlFragment ParseRequired(string sql)
    {
        var result = new ScriptDomSqlParser().Parse(sql);
        Assert.True(result.Success, string.Join("; ", result.Errors));
        return Assert.IsAssignableFrom<TSqlFragment>(result.SyntaxTree);
    }
}
