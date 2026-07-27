using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SQLGuardian.Domain;
using SQLGuardian.Reporting;
using SQLGuardian.Ssms.Services;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace SQLGuardian.Ssms;

public partial class MainWindow : Window
{
    private readonly SsmsAnalysisHost _host = new();
    private readonly List<string> _currentPaths = [];
    private string? _configPath;
    private AnalysisRun? _lastRun;
    private bool _busy;

    public MainWindow(LaunchOptions options)
    {
        InitializeComponent();
        LoadFormFromSettings(options);

        _currentPaths.AddRange(options.Paths);
        _configPath = options.ConfigPath
            ?? SsmsAnalysisHost.FindConfigNear(options.Paths.FirstOrDefault());

        UpdateConfigLabel();
        UpdateTargetLabel();
        OnAuthModeChanged(this, new RoutedEventArgs());

        if (_currentPaths.Count > 0)
        {
            _ = RunAnalysisAsync();
        }
    }

    private void LoadFormFromSettings(LaunchOptions options)
    {
        var settings = CompanionSettingsStore.Load();

        ServerBox.Text = settings.Server;
        DatabaseBox.Text = settings.Database;
        WindowsAuthBox.IsChecked = settings.UseWindowsAuthentication;
        UserBox.Text = settings.UserName;
        TrustCertBox.IsChecked = settings.TrustServerCertificate;
        RememberPasswordBox.IsChecked = settings.RememberPassword;
        if (settings.RememberPassword && !string.IsNullOrEmpty(settings.Password))
        {
            PasswordBox.Password = settings.Password;
        }

        // Launch args / env override saved form when provided as a full connection string.
        var connectionOverride = options.ConnectionString
            ?? Environment.GetEnvironmentVariable("SQLGUARDIAN_CONNECTION");
        if (!string.IsNullOrWhiteSpace(connectionOverride)
            && SqlConnectionForm.TryParse(connectionOverride, out var parsed))
        {
            ServerBox.Text = parsed.Server;
            DatabaseBox.Text = parsed.Database;
            WindowsAuthBox.IsChecked = parsed.UseWindowsAuthentication;
            UserBox.Text = parsed.UserName;
            if (!parsed.UseWindowsAuthentication)
            {
                PasswordBox.Password = parsed.Password;
            }

            TrustCertBox.IsChecked = parsed.TrustServerCertificate;
        }
    }

    private void OnAuthModeChanged(object sender, RoutedEventArgs e)
    {
        if (SqlAuthPanel is null || WindowsAuthBox is null)
        {
            return;
        }

        SqlAuthPanel.Visibility = WindowsAuthBox.IsChecked == true
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnAnalyzeFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SQL files (*.sql)|*.sql|All files (*.*)|*.*",
            Multiselect = true,
            Title = "Analyze SQL script(s)"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _currentPaths.Clear();
        _currentPaths.AddRange(dialog.FileNames);
        _configPath ??= SsmsAnalysisHost.FindConfigNear(_currentPaths[0]);
        UpdateConfigLabel();
        UpdateTargetLabel();
        _ = RunAnalysisAsync();
    }

    private void OnAnalyzeFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder of .sql scripts"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _currentPaths.Clear();
        _currentPaths.Add(dialog.FolderName);
        _configPath ??= SsmsAnalysisHost.FindConfigNear(dialog.FolderName);
        UpdateConfigLabel();
        UpdateTargetLabel();
        _ = RunAnalysisAsync();
    }

    private void OnReanalyze(object sender, RoutedEventArgs e) => _ = RunAnalysisAsync();

    private async void OnCatalogScan(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        if (!TryBuildConnection(out var connection, out var error))
        {
            MessageBox.Show(this, error, "SQLGuardian", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SaveSettings(lastScriptPath: null);

        try
        {
            if (ForceRefreshBox.IsChecked == true)
            {
                _host.InvalidateSchemaCache();
            }

            SetBusy(true, "Fast catalog scan (FKs + indexes + row counts)…");
            var configPath = _configPath;
            var run = await Task.Run(() => _host.AnalyzeCatalog(connection, configPath)).ConfigureAwait(true);
            ApplyRun(run, catalog: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "SQLGuardian catalog scan failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            StatusText.Text = "Catalog scan failed.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnExportMarkdown(object sender, RoutedEventArgs e)
    {
        if (_lastRun is null)
        {
            MessageBox.Show(
                this,
                "Run a scan or analysis first.",
                "SQLGuardian",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown (*.md)|*.md",
            FileName = "sqlguardian-report.md"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var markdown = new MarkdownReportWriter().Write(_lastRun, new ReportWriteOptions
        {
            BaseDirectory = IOPath.GetDirectoryName(dialog.FileName)
        });
        IOFile.WriteAllText(dialog.FileName, markdown);
        StatusText.Text = $"Exported: {dialog.FileName}";
    }

    private void OnIssueSelected(object sender, SelectionChangedEventArgs e)
    {
        if (IssuesGrid.SelectedItem is not IssueRow row)
        {
            return;
        }

        SuggestionText.Text = string.IsNullOrWhiteSpace(row.Suggestion)
            ? "No suggestion for this finding."
            : row.Suggestion;

        if (!string.IsNullOrWhiteSpace(row.SuggestedSql))
        {
            RecommendationTitle.Text = $"Recommended SQL — {row.RuleId}";
            RecommendationBox.Text =
                $"-- {row.RuleId}: {row.Suggestion}\r\n{row.SuggestedSql.Trim()}\r\n";
        }
        else if (_lastRun is not null)
        {
            ShowCombinedRecommendations();
        }
        else
        {
            RecommendationTitle.Text = "Recommended SQL";
            RecommendationBox.Text = "No ready-to-run SQL for this finding.";
        }
    }

    private void OnCopyRecommendation(object sender, RoutedEventArgs e)
    {
        var text = RecommendationBox.Text;
        if (string.IsNullOrWhiteSpace(text)
            || text.StartsWith("Select a finding", StringComparison.Ordinal)
            || text.StartsWith("No ready-to-run", StringComparison.Ordinal)
            || text.StartsWith("No recommended", StringComparison.Ordinal))
        {
            MessageBox.Show(
                this,
                "Nothing to copy yet. Run an analysis that produces SQL fixes.",
                "SQLGuardian",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(text);
        StatusText.Text = "Recommended SQL copied to clipboard.";
    }

    private void OnExportRecommendedSql(object sender, RoutedEventArgs e)
    {
        var text = RecommendationBox.Text;
        if (string.IsNullOrWhiteSpace(text)
            || text.StartsWith("Select a finding", StringComparison.Ordinal))
        {
            MessageBox.Show(
                this,
                "Run an analysis first.",
                "SQLGuardian",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "SQL script (*.sql)|*.sql",
            FileName = "sqlguardian-recommendations.sql"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        IOFile.WriteAllText(dialog.FileName, text);
        StatusText.Text = $"Exported recommendations: {dialog.FileName}";
    }

    private void OnIssueDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (IssuesGrid.SelectedItem is not IssueRow row)
        {
            return;
        }

        if (string.Equals(row.FullPath, "<catalog>", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = row.FullPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Could not open file:\n{ex.Message}",
                "SQLGuardian",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task RunAnalysisAsync()
    {
        if (_busy)
        {
            return;
        }

        if (_currentPaths.Count == 0)
        {
            StatusText.Text = "Choose a SQL file or folder to analyze.";
            return;
        }

        string? connection = null;
        if (!string.IsNullOrWhiteSpace(DatabaseBox.Text))
        {
            if (!TryBuildConnection(out connection, out var error))
            {
                var continueWithout = MessageBox.Show(
                    this,
                    error + "\n\nAnalyze scripts without schema recommendations?",
                    "SQLGuardian",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (continueWithout != MessageBoxResult.Yes)
                {
                    return;
                }

                connection = null;
            }
            else
            {
                SaveSettings(_currentPaths.FirstOrDefault());
            }
        }

        try
        {
            if (ForceRefreshBox.IsChecked == true)
            {
                _host.InvalidateSchemaCache();
            }

            SetBusy(true, string.IsNullOrWhiteSpace(connection)
                ? "Analyzing scripts…"
                : "Loading schema (parallel), then analyzing…");

            var paths = _currentPaths.ToList();
            var configPath = _configPath;
            var run = await Task.Run(() => _host.AnalyzePaths(paths, configPath, connection))
                .ConfigureAwait(true);
            ApplyRun(run, catalog: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "SQLGuardian analysis failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            StatusText.Text = "Analysis failed.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplyRun(AnalysisRun run, bool catalog)
    {
        _lastRun = run;
        IssuesGrid.ItemsSource = IssueRow.FromRun(run);
        ReanalyzeButton.IsEnabled = !catalog && _currentPaths.Count > 0;

        var schemaNote = catalog
            ? "  |  catalog scan"
            : (string.IsNullOrWhiteSpace(DatabaseBox.Text) ? "" : "  |  schema attached");

        SummaryText.Text =
            $"Targets: {run.FileCount}  |  Issues: {run.IssueCount}  |  Parse errors: {run.ParseErrorCount}{schemaNote}";
        StatusText.Text = run.IssueCount == 0
            ? (catalog ? "Catalog scan complete — no schema issues." : "Analysis complete — no issues.")
            : $"{run.IssueCount} issue(s) found. Select a row for the suggested SQL fix.";

        ShowCombinedRecommendations();
    }

    private void ShowCombinedRecommendations()
    {
        if (_lastRun is null)
        {
            return;
        }

        var combined = string.Join(
            Environment.NewLine + Environment.NewLine,
            _lastRun.Reports
                .Where(r => !string.IsNullOrWhiteSpace(r.RecommendedSql))
                .Select(r => r.RecommendedSql!.TrimEnd()));

        RecommendationTitle.Text = "Recommended SQL — all fixes";
        RecommendationBox.Text = string.IsNullOrWhiteSpace(combined)
            ? "No recommended SQL for this run. Connect to a database for index rewrites, or fix SELECT * / JOIN issues with schema attached."
            : combined + Environment.NewLine;
    }

    private bool TryBuildConnection(out string connection, out string error)
    {
        var form = new SqlConnectionForm
        {
            Server = ServerBox.Text ?? string.Empty,
            Database = DatabaseBox.Text ?? string.Empty,
            UseWindowsAuthentication = WindowsAuthBox.IsChecked == true,
            UserName = UserBox.Text ?? string.Empty,
            Password = PasswordBox.Password,
            TrustServerCertificate = TrustCertBox.IsChecked == true
        };

        if (!form.IsValid(out error))
        {
            connection = string.Empty;
            return false;
        }

        connection = form.ToConnectionString();
        return true;
    }

    private void SaveSettings(string? lastScriptPath)
    {
        CompanionSettingsStore.Save(new CompanionSettings
        {
            Server = ServerBox.Text?.Trim() ?? ".",
            Database = DatabaseBox.Text?.Trim() ?? string.Empty,
            UseWindowsAuthentication = WindowsAuthBox.IsChecked == true,
            UserName = UserBox.Text?.Trim() ?? string.Empty,
            TrustServerCertificate = TrustCertBox.IsChecked == true,
            RememberPassword = RememberPasswordBox.IsChecked == true,
            Password = PasswordBox.Password,
            LastScriptPath = lastScriptPath
        });
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        CatalogButton.IsEnabled = !busy;
        ReanalyzeButton.IsEnabled = !busy && _currentPaths.Count > 0;
        if (status is not null)
        {
            StatusText.Text = status;
        }
    }

    private void UpdateConfigLabel() =>
        ConfigText.Text = string.IsNullOrWhiteSpace(_configPath)
            ? "Config: (defaults)"
            : $"Config: {IOPath.GetFileName(_configPath)}";

    private void UpdateTargetLabel()
    {
        if (_currentPaths.Count == 0)
        {
            TargetText.Text = "No scripts selected yet.";
            ReanalyzeButton.IsEnabled = false;
            return;
        }

        if (_currentPaths.Count == 1)
        {
            TargetText.Text = $"Last target: {_currentPaths[0]}";
        }
        else
        {
            TargetText.Text = $"Last target: {_currentPaths.Count} path(s)";
        }

        ReanalyzeButton.IsEnabled = !_busy;
    }
}
