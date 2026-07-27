using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SQLGuardian.ScriptDom.Tests;

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
        Assert.IsAssignableFrom<TSqlFragment>(result.SyntaxTree);
    }

    [Fact]
    public void Parse_InvalidSql_ReportsErrors()
    {
        var parser = new ScriptDomSqlParser();
        var result = parser.Parse("SELECT FROM;");

        Assert.NotEmpty(result.Errors);
        Assert.False(result.Success);
    }

    [Fact]
    public void Parse_ToAnalysisContext_PreservesTree()
    {
        var result = new ScriptDomSqlParser().Parse("SELECT 1;", "a.sql");
        var context = result.ToAnalysisContext();

        Assert.Equal("a.sql", context.FilePath);
        Assert.True(ScriptDomSyntax.TryGetFragment(context, out var fragment));
        Assert.Same(result.SyntaxTree, fragment);
    }

    [Fact]
    public void FragmentExtensions_ProducesSourceLocation()
    {
        var fragment = SampleSql.ParseRequired("SELECT 1;");
        var location = fragment.GetSourceLocation();

        Assert.True(location.StartLine >= 1);
        Assert.True(location.StartColumn >= 1);
        Assert.True(location.EndLine >= location.StartLine);
    }
}
