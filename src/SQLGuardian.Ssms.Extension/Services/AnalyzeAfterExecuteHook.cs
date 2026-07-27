using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SQLGuardian.Ssms.Extension;

/// <summary>
/// Runs SQLGuardian analysis after SSMS Query → Execute (F5),
/// and shows cancelable pre-execute guards for missing WHERE, large reads, and large joins.
/// Must keep a rooted <see cref="CommandEvents"/> reference so COM does not collect it.
/// </summary>
internal sealed class AnalyzeAfterExecuteHook : IDisposable
{
    // Classic SSMS "Query.Execute" command (still used by SSMS 21 shell).
    private const string SsmsExecuteCommandGuid = "{52692960-56BC-4989-B5D3-94C47A513E8D}";
    private const int SsmsExecuteCommandId = 1;
    private const int IdOk = 1;

    private readonly SQLGuardianPackage _package;
    private CommandEvents? _executeEvents;
    private bool _running;
    private bool _prechecking;

    private AnalyzeAfterExecuteHook(SQLGuardianPackage package)
    {
        _package = package;
    }

    public static async System.Threading.Tasks.Task<AnalyzeAfterExecuteHook?> TryInitializeAsync(SQLGuardianPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var dte = await package.GetServiceAsync(typeof(DTE)) as DTE;
        if (dte?.Events is null)
        {
            return null;
        }

        var hook = new AnalyzeAfterExecuteHook(package);
        try
        {
            // Strong reference required — otherwise GC can drop the sink and events stop firing.
            hook._executeEvents = dte.Events.CommandEvents[SsmsExecuteCommandGuid, SsmsExecuteCommandId];
            hook._executeEvents.BeforeExecute += hook.OnBeforeExecute;
            hook._executeEvents.AfterExecute += hook.OnAfterExecute;
            return hook;
        }
        catch
        {
            hook.Dispose();
            return null;
        }
    }

    private void OnBeforeExecute(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            if (_prechecking || _running)
            {
                return;
            }

            var options = (GeneralOptionPage)_package.GetDialogPage(typeof(GeneralOptionPage));
            var anyGuard = options.WarnBeforeMissingWhere
                           || options.WarnBeforeLargeSelect
                           || options.WarnBeforeLargeJoin;
            if (!anyGuard)
            {
                return;
            }

            var dte = (DTE?)Package.GetGlobalService(typeof(DTE));
            var document = dte?.ActiveDocument;
            if (document is null || !LooksLikeSqlDocument(document))
            {
                return;
            }

            var text = GetTextForExecute(document);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // Connection is optional: missing WHERE still works without it.
            string? connection = null;
            _ = ConnectionSettingsResolver.TryResolveConnectionString(
                _package,
                options,
                document,
                out connection,
                out _,
                out _);

            _prechecking = true;
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "SQLGuardian", "ssms");
                Directory.CreateDirectory(tempDir);
                var analyzePath = Path.Combine(tempDir, $"precheck-{Guid.NewGuid():N}.sql");
                File.WriteAllText(analyzePath, text);

                CliPrecheckResult result;
                try
                {
                    var threshold = options.LargeTableRowThreshold > 0
                        ? options.LargeTableRowThreshold
                        : 1_000_000L;
                    result = CliAnalyzer.PrecheckFile(
                        analyzePath,
                        connection,
                        threshold,
                        options.CliPathOverride);
                }
                finally
                {
                    try { File.Delete(analyzePath); } catch { /* ignore */ }
                }

                // Fail open: connection/CLI problems must not block Execute.
                if (!result.Success)
                {
                    return;
                }

                var snapshot = result.Snapshot;

                if (options.WarnBeforeMissingWhere
                    && snapshot.MissingWhere.Count > 0
                    && !ConfirmMissingWhere(snapshot))
                {
                    cancelDefault = true;
                    return;
                }

                if (options.WarnBeforeLargeSelect
                    && snapshot.LargeReads.Count > 0
                    && !ConfirmLargeReads(snapshot))
                {
                    cancelDefault = true;
                    return;
                }

                if (options.WarnBeforeLargeJoin && snapshot.LargeJoins.Count > 0)
                {
                    var joinChoice = ConfirmLargeJoins(snapshot, options.AllowNolockQuickFixOnLargeJoins);
                    if (joinChoice == LargeJoinDialogResult.Cancel)
                    {
                        cancelDefault = true;
                        return;
                    }

                    if (joinChoice == LargeJoinDialogResult.ApplyNolock)
                    {
                        if (!string.IsNullOrWhiteSpace(snapshot.SuggestedNolockSql)
                            && ReplaceExecuteText(document, snapshot.SuggestedNolockSql!))
                        {
                            VsShellUtilities.ShowMessageBox(
                                _package,
                                "WITH (NOLOCK) was applied to large joined tables. Review the script, then Execute again.\n\nNote: NOLOCK allows dirty reads. SQLG0003 may still flag this in the Error List.",
                                "SQLGuardian — NOLOCK applied",
                                OLEMSGICON.OLEMSGICON_INFO,
                                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                        }

                        // Always cancel Execute after Apply so the user can review.
                        cancelDefault = true;
                    }
                }
            }
            finally
            {
                _prechecking = false;
            }
        }
        catch
        {
            _prechecking = false;
            // Fail open on unexpected errors.
        }
    }

    private bool ConfirmMissingWhere(ExecuteGuardSnapshot snapshot)
    {
        var body = new StringBuilder();
        body.AppendLine("SQLGuardian detected UPDATE/DELETE without WHERE (SQLG0002):");
        body.AppendLine();
        foreach (var warning in snapshot.MissingWhere.Take(5))
        {
            body.AppendLine("• " + warning.Message);
        }

        if (snapshot.MissingWhere.Count > 5)
        {
            body.AppendLine($"• …and {snapshot.MissingWhere.Count - 5} more.");
        }

        body.AppendLine();
        body.Append("This can modify or delete every row. Continue and execute anyway?");

        var answer = VsShellUtilities.ShowMessageBox(
            _package,
            body.ToString(),
            "SQLGuardian — missing WHERE",
            OLEMSGICON.OLEMSGICON_CRITICAL,
            OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

        return answer == IdOk;
    }

    private bool ConfirmLargeReads(ExecuteGuardSnapshot snapshot)
    {
        var body = new StringBuilder();
        body.AppendLine("SQLGuardian detected a potentially expensive read:");
        body.AppendLine();
        foreach (var warning in snapshot.LargeReads.Take(5))
        {
            body.AppendLine("• " + warning.Message);
        }

        if (snapshot.LargeReads.Count > 5)
        {
            body.AppendLine($"• …and {snapshot.LargeReads.Count - 5} more.");
        }

        body.AppendLine();
        body.Append("Continue and execute this script?");

        var answer = VsShellUtilities.ShowMessageBox(
            _package,
            body.ToString(),
            "SQLGuardian — large table warning",
            OLEMSGICON.OLEMSGICON_WARNING,
            OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

        return answer == IdOk;
    }

    private LargeJoinDialogResult ConfirmLargeJoins(ExecuteGuardSnapshot snapshot, bool allowNolock)
    {
        var body = new StringBuilder();
        body.AppendLine("SQLGuardian detected a join involving large table(s):");
        body.AppendLine();
        foreach (var warning in snapshot.LargeJoins.Take(5))
        {
            body.AppendLine("• " + warning.Message);
        }

        if (snapshot.LargeJoins.Count > 5)
        {
            body.AppendLine($"• …and {snapshot.LargeJoins.Count - 5} more.");
        }

        body.AppendLine();
        body.Append(allowNolock && !string.IsNullOrWhiteSpace(snapshot.SuggestedNolockSql)
            ? "Cancel, execute anyway, or apply WITH (NOLOCK) to large joined tables (advanced)."
            : "Cancel stops Execute. Execute anyway continues.");

        using var dialog = new LargeJoinWarningDialog(
            body.ToString(),
            allowNolock && !string.IsNullOrWhiteSpace(snapshot.SuggestedNolockSql));
        dialog.ShowDialog();
        return dialog.Choice;
    }

    private static bool ReplaceDocumentText(Document document, string newText)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            if (document.Object("TextDocument") is not TextDocument textDocument)
            {
                return false;
            }

            var editPoint = textDocument.StartPoint.CreateEditPoint();
            editPoint.ReplaceText(textDocument.EndPoint, newText, (int)vsEPReplaceTextOptions.vsEPReplaceTextKeepMarkers);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OnAfterExecute(string guid, int id, object customIn, object customOut)
    {
        _ = OnAfterExecuteAsync();
    }

    private async System.Threading.Tasks.Task OnAfterExecuteAsync()
    {
        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var options = (GeneralOptionPage)_package.GetDialogPage(typeof(GeneralOptionPage));
            if (!options.AnalyzeAfterExecute)
            {
                return;
            }

            if (_running)
            {
                return;
            }

            _running = true;
            try
            {
                // Yield so SSMS can finish updating results panes before we analyze.
                await System.Threading.Tasks.Task.Yield();
                await ActiveDocumentAnalyzer.AnalyzeAsync(_package, silent: true);
            }
            finally
            {
                _running = false;
            }
        }
        catch
        {
            _running = false;
        }
    }

    private static bool LooksLikeSqlDocument(Document document)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var name = document.Name ?? string.Empty;
        if (name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var lang = document.Language ?? string.Empty;
        return lang.IndexOf("SQL", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Prefer the current selection when present (matches SSMS Execute-selection behavior).
    /// Falls back to the full document when nothing is selected.
    /// </summary>
    private static string GetTextForExecute(Document document)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            if (document.Selection is TextSelection selection)
            {
                var selected = selection.Text;
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    return selected;
                }
            }
        }
        catch
        {
            // Fall through to full document.
        }

        return GetDocumentText(document);
    }

    private static bool ReplaceExecuteText(Document document, string newText)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            // If the user had a selection, replace only that range (precheck was selection-scoped).
            if (document.Selection is TextSelection selection
                && !string.IsNullOrWhiteSpace(selection.Text))
            {
                selection.Insert(newText, (int)vsInsertFlags.vsInsertFlagsContainNewText);
                return true;
            }

            return ReplaceDocumentText(document, newText);
        }
        catch
        {
            return false;
        }
    }

    private static string GetDocumentText(Document document)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (document.Object("TextDocument") is not TextDocument textDocument)
        {
            return string.Empty;
        }

        var editPoint = textDocument.StartPoint.CreateEditPoint();
        return editPoint.GetText(textDocument.EndPoint) ?? string.Empty;
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_executeEvents is not null)
        {
            try
            {
                _executeEvents.BeforeExecute -= OnBeforeExecute;
                _executeEvents.AfterExecute -= OnAfterExecute;
            }
            catch
            {
                // ignore during shutdown
            }

            _executeEvents = null;
        }
    }
}
