using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace SQLGuardian.Ssms.Extension;

/// <summary>
/// Pushes SQLGuardian findings into the SSMS / VS Error List.
/// Double-click navigates the active query window (or opens the file when saved).
/// </summary>
public sealed class ErrorListGateway : IDisposable
{
    private ErrorListProvider? _provider;
    private Document? _lastDocument;

    public void ReplaceFindings(
        IReadOnlyList<GuardianFinding> findings,
        string documentPath,
        Document? document)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        EnsureProvider();
        _provider!.Tasks.Clear();
        _lastDocument = document;

        foreach (var finding in findings.OrderBy(f => f.StartLine).ThenBy(f => f.StartColumn))
        {
            var task = new ErrorTask
            {
                ErrorCategory = MapSeverity(finding.Severity),
                Category = TaskCategory.BuildCompile,
                Text = FormatText(finding),
                Document = string.IsNullOrWhiteSpace(documentPath) || documentPath == "(active script)"
                    ? finding.FilePath ?? string.Empty
                    : documentPath,
                Line = Math.Max(0, finding.StartLine - 1),
                Column = Math.Max(0, finding.StartColumn - 1),
                CanDelete = true,
                HelpKeyword = finding.RuleId,
                Priority = finding.Severity is "Critical" or "High"
                    ? TaskPriority.High
                    : TaskPriority.Normal
            };

            task.Navigate += OnNavigate;
            _provider.Tasks.Add(task);
        }

        _provider.Show();
        _provider.BringToFront();
    }

    public void Clear()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_provider is null)
        {
            return;
        }

        _provider.Tasks.Clear();
    }

    public async Task BringToFrontAsync(SQLGuardianPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        EnsureProvider();
        _provider!.BringToFront();

        if (await package.GetServiceAsync(typeof(SVsErrorList)) is IVsErrorList errorList)
        {
            errorList.BringToFront();
        }
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _provider?.Dispose();
        _provider = null;
    }

    private void EnsureProvider()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_provider is not null)
        {
            return;
        }

        var package = SQLGuardianPackage.Instance
            ?? throw new InvalidOperationException("SQLGuardian package is not initialized.");
        _provider = new ErrorListProvider(package)
        {
            ProviderName = "SQLGuardian",
            ProviderGuid = new Guid("c1d2e3f4-a5b6-4789-8c0d-1e2f3a4b5c6d")
        };
    }

    private void OnNavigate(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is not ErrorTask task)
        {
            return;
        }

        // Prefer jumping in the original active query window (works for unsaved buffers).
        if (_lastDocument?.Object("TextDocument") is TextDocument textDocument)
        {
            try
            {
                var line = Math.Max(1, task.Line + 1);
                var column = Math.Max(1, task.Column + 1);
                var point = textDocument.StartPoint.CreateEditPoint();
                point.MoveToLineAndOffset(line, column);
                point.TryToShow(vsPaneShowHow.vsPaneShowCentered, null);
                _lastDocument.Activate();
                return;
            }
            catch
            {
                // Fall through to default file navigation.
            }
        }

        if (!string.IsNullOrWhiteSpace(task.Document) && File.Exists(task.Document))
        {
            VsShellUtilities.OpenDocument(SQLGuardianPackage.Instance, task.Document);
        }
    }

    private static TaskErrorCategory MapSeverity(string? severity) =>
        severity switch
        {
            "Critical" or "High" => TaskErrorCategory.Error,
            // Medium / Low / Info (and anything else) → Warnings tab (not Messages).
            _ => TaskErrorCategory.Warning
        };

    private static string FormatText(GuardianFinding finding)
    {
        var text = $"{finding.RuleId}: {finding.Message}";
        if (!string.IsNullOrWhiteSpace(finding.Suggestion))
        {
            text += $" — {finding.Suggestion}";
        }

        if (!string.IsNullOrWhiteSpace(finding.SuggestedSql))
        {
            var oneLine = finding.SuggestedSql!
                .Replace("\r\n", " ")
                .Replace('\n', ' ')
                .Trim();
            if (oneLine.Length > 160)
            {
                oneLine = oneLine.Substring(0, 157) + "...";
            }

            text += $" | SQL: {oneLine}";
        }

        return text;
    }
}
