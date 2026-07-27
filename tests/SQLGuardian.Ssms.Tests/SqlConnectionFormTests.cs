using SQLGuardian.Ssms.Services;

namespace SQLGuardian.Ssms.Tests;

public class SqlConnectionFormTests
{
    [Fact]
    public void ToConnectionString_WindowsAuth()
    {
        var form = new SqlConnectionForm
        {
            Server = "localhost",
            Database = "AdventureWorks",
            UseWindowsAuthentication = true,
            TrustServerCertificate = true
        };

        Assert.True(form.IsValid(out _));
        var cs = form.ToConnectionString();
        Assert.Contains("Server=localhost", cs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Database=AdventureWorks", cs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Trusted_Connection=True", cs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsValid_RequiresDatabase()
    {
        var form = new SqlConnectionForm { Server = ".", Database = "" };
        Assert.False(form.IsValid(out var error));
        Assert.Contains("database", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseConnectionString_RoundTripsBasics()
    {
        var ok = SqlConnectionForm.TryParse(
            "Server=.;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True",
            out var form);

        Assert.True(ok);
        Assert.Equal(".", form.Server);
        Assert.Equal("MyDb", form.Database);
        Assert.True(form.UseWindowsAuthentication);
    }
}
