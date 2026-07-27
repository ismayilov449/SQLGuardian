using SQLGuardian.Abstractions;
using SQLGuardian.RuleEngine;
using SQLGuardian.RuleEngine.Rules;

namespace SQLGuardian.RuleEngine.Tests;

public class RuleCatalogTests
{
    [Fact]
    public void CreateDefault_DiscoversFirstPack_InIdOrder()
    {
        var rules = RuleCatalog.CreateDefault();

        Assert.Equal(22, rules.Count);
        Assert.Equal(
            [
                "SQLG0001", "SQLG0002", "SQLG0003", "SQLG0004", "SQLG0005",
                "SQLG0006", "SQLG0007", "SQLG0008", "SQLG0009", "SQLG0010",
                "SQLG0011", "SQLG0012", "SQLG0013", "SQLG0014", "SQLG0015",
                "SQLG0016", "SQLG0017", "SQLG0018", "SQLG0019", "SQLG0020",
                "SQLG0021", "SQLG0022"
            ],
            rules.Select(r => r.RuleId).ToArray());
    }
}

public class RuleConfigurationLoaderTests
{
    [Fact]
    public void LoadJson_SupportsRuleId_ClassName_AndDisabled()
    {
        var rules = RuleCatalog.CreateDefault();

        var flat = RuleConfigurationLoader.LoadJson("""
            {
              "SelectStarRule": "Error",
              "SQLG0002": "Disabled",
              "SQLG0003": "Warning"
            }
            """, rules);

        Assert.False(flat.IsEnabled("SQLG0002"));
        Assert.Equal(Severity.High, flat.ResolveSeverity("SQLG0001", Severity.Medium));
        Assert.Equal(Severity.Medium, flat.ResolveSeverity("SQLG0003", Severity.High));

        var nested = RuleConfigurationLoader.LoadJson("""
            {
              "rules": {
                "SQLG0009": "Disabled"
              }
            }
            """, rules);

        Assert.False(nested.IsEnabled("SQLG0009"));
    }
}

public class SelectStarRuleTests
{
    private readonly SelectStarRule _rule = new();

    [Fact]
    public void Flags_SelectStar() =>
        RuleTestHelper.AssertHasIssue(_rule, "SELECT * FROM dbo.Users;");

    [Fact]
    public void Allows_ExplicitColumns() =>
        RuleTestHelper.AssertNoIssue(_rule, "SELECT Id, Name FROM dbo.Users;");

    [Fact]
    public void DoesNotFlag_CountStar() =>
        RuleTestHelper.AssertNoIssue(_rule, "SELECT COUNT(*) FROM dbo.Users;");
}

public class MissingWhereRuleTests
{
    private readonly MissingWhereRule _rule = new();

    [Fact]
    public void Flags_DeleteWithoutWhere() =>
        RuleTestHelper.AssertHasIssue(_rule, "DELETE FROM dbo.Users;");

    [Fact]
    public void Flags_UpdateWithoutWhere() =>
        RuleTestHelper.AssertHasIssue(_rule, "UPDATE dbo.Users SET IsActive = 0;");

    [Fact]
    public void Allows_DeleteWithWhere() =>
        RuleTestHelper.AssertNoIssue(_rule, "DELETE FROM dbo.Users WHERE Id = 1;");
}

public class NoLockRuleTests
{
    private readonly NoLockRule _rule = new();

    [Fact]
    public void Flags_NoLockHint() =>
        RuleTestHelper.AssertHasIssue(_rule, "SELECT Id FROM dbo.Users WITH (NOLOCK);");

    [Fact]
    public void Allows_NoHint() =>
        RuleTestHelper.AssertNoIssue(_rule, "SELECT Id FROM dbo.Users;");
}

public class TopWithoutOrderRuleTests
{
    private readonly TopWithoutOrderRule _rule = new();

    [Fact]
    public void Flags_TopWithoutOrderBy() =>
        RuleTestHelper.AssertHasIssue(_rule, "SELECT TOP 10 Id FROM dbo.Users;");

    [Fact]
    public void Allows_TopWithOrderBy() =>
        RuleTestHelper.AssertNoIssue(_rule, "SELECT TOP 10 Id FROM dbo.Users ORDER BY Id;");
}

public class LikeLeadingWildcardRuleTests
{
    private readonly LikeLeadingWildcardRule _rule = new();

    [Fact]
    public void Flags_LeadingPercent() =>
        RuleTestHelper.AssertHasIssue(_rule, "SELECT Id FROM dbo.Products WHERE Name LIKE '%widget%';");

    [Fact]
    public void Allows_TrailingOnlyWildcard() =>
        RuleTestHelper.AssertNoIssue(_rule, "SELECT Id FROM dbo.Products WHERE Name LIKE 'widget%';");
}

public class CrossJoinRuleTests
{
    private readonly CrossJoinRule _rule = new();

    [Fact]
    public void Flags_CrossJoin() =>
        RuleTestHelper.AssertHasIssue(_rule, "SELECT * FROM dbo.Users CROSS JOIN dbo.Regions;");

    [Fact]
    public void Allows_InnerJoin() =>
        RuleTestHelper.AssertNoIssue(
            _rule,
            "SELECT u.Id FROM dbo.Users AS u INNER JOIN dbo.Orders AS o ON o.UserId = u.Id;");
}

public class CursorRuleTests
{
    private readonly CursorRule _rule = new();

    [Fact]
    public void Flags_DeclareCursor() =>
        RuleTestHelper.AssertHasIssue(
            _rule,
            """
            DECLARE c CURSOR FOR SELECT Id FROM dbo.Users;
            OPEN c;
            FETCH NEXT FROM c INTO @Id;
            CLOSE c;
            DEALLOCATE c;
            """);

    [Fact]
    public void Allows_SetBasedUpdate() =>
        RuleTestHelper.AssertNoIssue(_rule, "UPDATE dbo.Users SET IsActive = 1 WHERE IsActive = 0;");
}

public class UnionRuleTests
{
    private readonly UnionRule _rule = new();

    [Fact]
    public void Flags_Union() =>
        RuleTestHelper.AssertHasIssue(
            _rule,
            "SELECT Id FROM dbo.A UNION SELECT Id FROM dbo.B;");

    [Fact]
    public void Allows_UnionAll() =>
        RuleTestHelper.AssertNoIssue(
            _rule,
            "SELECT Id FROM dbo.A UNION ALL SELECT Id FROM dbo.B;");
}

public class DistinctRuleTests
{
    private readonly DistinctRule _rule = new();

    [Fact]
    public void Flags_Distinct() =>
        RuleTestHelper.AssertHasIssue(_rule, "SELECT DISTINCT Email FROM dbo.Users;");

    [Fact]
    public void Allows_WithoutDistinct() =>
        RuleTestHelper.AssertNoIssue(_rule, "SELECT Email FROM dbo.Users;");
}

public class WaitForDelayRuleTests
{
    private readonly WaitForDelayRule _rule = new();

    [Fact]
    public void Flags_WaitForDelay() =>
        RuleTestHelper.AssertHasIssue(_rule, "WAITFOR DELAY '00:00:05';");

    [Fact]
    public void Allows_OrdinarySelect() =>
        RuleTestHelper.AssertNoIssue(_rule, "SELECT 1;");
}
