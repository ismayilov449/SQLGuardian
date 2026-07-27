using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace SQLGuardian.Ssms.Extension;

[Guid("ce1eea61-e087-365f-b3da-cbc0c58a01d5")]
public sealed class GeneralOptionPage : DialogPage
{
    [Category("Connection")]
    [DisplayName("Use active SSMS connection")]
    [Description("Use the active query window's SSMS connection automatically for schema-aware analysis and pre-execute warnings.")]
    [DefaultValue(true)]
    public bool UseActiveDocumentConnection { get; set; } = true;

    [Category("Connection")]
    [DisplayName("Server")]
    [Description("Fallback saved profile when active SSMS connection detection is unavailable.")]
    public string ServerName { get; set; } = string.Empty;

    [Category("Connection")]
    [DisplayName("Database")]
    [Description("Fallback saved profile database name.")]
    public string DatabaseName { get; set; } = string.Empty;

    [Category("Connection")]
    [DisplayName("Use Windows authentication")]
    [Description("Use Integrated Security for the fallback saved profile.")]
    [DefaultValue(true)]
    public bool UseWindowsAuthentication { get; set; } = true;

    [Category("Connection")]
    [DisplayName("SQL user name")]
    [Description("Used only when Windows authentication is off for the fallback saved profile.")]
    public string SqlUserName { get; set; } = string.Empty;

    [Category("Connection")]
    [PasswordPropertyText(true)]
    [DisplayName("SQL password")]
    [Description("Used only when Windows authentication is off for the fallback saved profile.")]
    public string SqlPassword { get; set; } = string.Empty;

    [Category("Connection")]
    [DisplayName("Trust server certificate")]
    [Description("Adds TrustServerCertificate=True to the fallback saved profile connection string.")]
    [DefaultValue(true)]
    public bool TrustServerCertificate { get; set; } = true;

    [Category("Analysis")]
    [DisplayName("Connection string")]
    [Description("Legacy advanced fallback connection string. Normally leave empty and use the active SSMS connection or the friendly fields above.")]
    [Browsable(false)]
    public string ConnectionString { get; set; } = string.Empty;

    [Category("Analysis")]
    [DisplayName("CLI path override")]
    [Description("Advanced troubleshooting only. Empty = use the CLI bundled with the extension.")]
    [Browsable(false)]
    public string CliPathOverride { get; set; } = string.Empty;

    [Category("Analysis")]
    [DisplayName("Analyze after Execute")]
    [Description("When enabled, run SQLGuardian on the active script after Query → Execute (F5) and push findings to the Error List.")]
    public bool AnalyzeAfterExecute { get; set; } = true;

    [Category("Execute guard")]
    [DisplayName("Warn before UPDATE/DELETE without WHERE")]
    [Description("Before Query → Execute, warn when UPDATE or DELETE has no WHERE clause (SQLG0002). Cancel stops Execute.")]
    [DefaultValue(true)]
    public bool WarnBeforeMissingWhere { get; set; } = true;

    [Category("Execute guard")]
    [DisplayName("Warn before large SELECT")]
    [Description("Before Query → Execute, warn when SELECT * or unbounded SELECT targets a table whose approximate row count meets the threshold.")]
    [DefaultValue(true)]
    public bool WarnBeforeLargeSelect { get; set; } = true;

    [Category("Execute guard")]
    [DisplayName("Warn before large JOIN")]
    [Description("Before Query → Execute, warn when a JOIN involves a table whose approximate row count meets the threshold.")]
    [DefaultValue(true)]
    public bool WarnBeforeLargeJoin { get; set; } = true;

    [Category("Execute guard")]
    [DisplayName("Allow NOLOCK quick-fix on large joins")]
    [Description("Advanced and off by default. When enabled, the large-join warning can apply WITH (NOLOCK) to large joined tables. Dirty reads are possible. SQLG0003 still warns about NOLOCK in the Error List.")]
    [DefaultValue(false)]
    public bool AllowNolockQuickFixOnLargeJoins { get; set; } = false;

    [Category("Execute guard")]
    [DisplayName("Large table row threshold")]
    [Description("Approximate catalog row count at or above which large SELECT / JOIN pre-execute warnings trigger (default 1,000,000).")]
    public long LargeTableRowThreshold { get; set; } = 1_000_000;
}
