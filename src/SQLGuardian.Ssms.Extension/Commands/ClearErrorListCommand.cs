using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace SQLGuardian.Ssms.Extension;

internal sealed class ClearErrorListCommand
{
    private readonly SQLGuardianPackage _package;

    private ClearErrorListCommand(SQLGuardianPackage package)
    {
        _package = package;
    }

    public static async Task InitializeAsync(SQLGuardianPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
            ?? throw new InvalidOperationException("Cannot get menu command service.");

        var command = new ClearErrorListCommand(package);
        var menuCommand = new OleMenuCommand(
            (_, __) => command.Execute(),
            new CommandID(PackageGuids.CommandSet, PackageIds.ClearErrorListCommand));
        menuCommand.BeforeQueryStatus += (_, __) =>
        {
            menuCommand.Visible = true;
            menuCommand.Enabled = true;
        };
        commandService.AddCommand(menuCommand);
    }

    private void Execute()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _package.ErrorList.Clear();
    }
}
