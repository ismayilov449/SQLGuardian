using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace SQLGuardian.Ssms.Extension;

/// <summary>
/// Shared active-document analysis used by the Tools command and the after-Execute hook.
/// </summary>
internal static class ActiveDocumentAnalyzer
{
    public static async Task AnalyzeAsync(SQLGuardianPackage package, bool silent)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        try
        {
            var dte = await package.GetServiceAsync(typeof(DTE)) as DTE;
            if (dte?.ActiveDocument is null)
            {
                if (!silent)
                {
                    ShowInfo(package, "Open a T-SQL script window first.");
                }

                return;
            }

            var document = dte.ActiveDocument;
            if (!LooksLikeSqlDocument(document))
            {
                return;
            }

            var text = GetDocumentText(document);
            if (string.IsNullOrWhiteSpace(text))
            {
                if (!silent)
                {
                    ShowInfo(package, "The active document is empty.");
                }

                return;
            }

            var navigationPath = document.FullName;
            string analyzePath;
            var deleteTemp = false;

            if (!string.IsNullOrWhiteSpace(navigationPath) && document.Saved && File.Exists(navigationPath))
            {
                analyzePath = navigationPath;
            }
            else
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "SQLGuardian", "ssms");
                Directory.CreateDirectory(tempDir);
                analyzePath = Path.Combine(tempDir, $"active-{Guid.NewGuid():N}.sql");
                File.WriteAllText(analyzePath, text);
                deleteTemp = true;
                if (string.IsNullOrWhiteSpace(navigationPath))
                {
                    navigationPath = "(active script)";
                }
            }

            var options = (GeneralOptionPage)package.GetDialogPage(typeof(GeneralOptionPage));
            await SetStatusAsync(package, "SQLGuardian: analyzing…");

            _ = ConnectionSettingsResolver.TryResolveConnectionString(
                package,
                options,
                document,
                out var connection,
                out _,
                out _);
            var cliOverride = options.CliPathOverride;
            var pathForAnalyze = analyzePath;
            var navPath = navigationPath;
            var docRef = document;

            var result = await Task.Run(() =>
                CliAnalyzer.AnalyzeFile(pathForAnalyze, connection, cliOverride));

            if (deleteTemp)
            {
                try { File.Delete(pathForAnalyze); } catch { /* ignore */ }
            }

            if (!result.Success)
            {
                await SetStatusAsync(package, "SQLGuardian: analysis failed.");
                if (!silent)
                {
                    ShowError(package, result.ErrorMessage ?? "Analysis failed.");
                }

                return;
            }

            package.ErrorList.ReplaceFindings(result.Findings, navPath, docRef);
            await package.ErrorList.BringToFrontAsync(package);

            var count = result.Findings.Count;
            await SetStatusAsync(package, count == 0
                ? "SQLGuardian: no issues."
                : string.Format(CultureInfo.CurrentCulture, "SQLGuardian: {0} finding(s) in Error List.", count));
        }
        catch (Exception ex)
        {
            await SetStatusAsync(package, "SQLGuardian: error.");
            if (!silent)
            {
                ShowError(package, ex.Message);
            }
        }
    }

    private static bool LooksLikeSqlDocument(Document document)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            var name = document.Name ?? string.Empty;
            var lang = document.Language ?? string.Empty;
            if (name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // SSMS query windows are often unsaved ("SQLQuery1.sql") or language "SQL".
            if (lang.IndexOf("SQL", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return document.Object("TextDocument") is TextDocument;
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
        return editPoint.GetText(textDocument.EndPoint);
    }

    private static async Task SetStatusAsync(SQLGuardianPackage package, string text)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (await package.GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar is { } statusbar)
        {
            statusbar.SetText(text);
        }
    }

    private static void ShowInfo(SQLGuardianPackage package, string message) =>
        VsShellUtilities.ShowMessageBox(
            package,
            message,
            "SQLGuardian",
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

    private static void ShowError(SQLGuardianPackage package, string message) =>
        VsShellUtilities.ShowMessageBox(
            package,
            message,
            "SQLGuardian",
            OLEMSGICON.OLEMSGICON_CRITICAL,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
}
