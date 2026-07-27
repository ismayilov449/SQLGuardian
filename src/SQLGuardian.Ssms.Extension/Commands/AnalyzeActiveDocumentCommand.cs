using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace SQLGuardian.Ssms.Extension;

internal static class AnalyzeActiveDocumentCommand
{
    public static async Task InitializeAsync(SQLGuardianPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
            ?? throw new InvalidOperationException("Cannot get menu command service.");

        var menuCommand = new OleMenuCommand(
            (_, __) => _ = ActiveDocumentAnalyzer.AnalyzeAsync(package, silent: false),
            new CommandID(PackageGuids.CommandSet, PackageIds.AnalyzeActiveDocumentCommand));
        menuCommand.BeforeQueryStatus += (_, __) =>
        {
            menuCommand.Visible = true;
            menuCommand.Enabled = true;
        };
        commandService.AddCommand(menuCommand);
    }
}
