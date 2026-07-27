using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace SQLGuardian.Ssms.Extension;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("SQLGuardian for SSMS", "Deterministic T-SQL static analysis in the Error List", "0.1.8")]
[ProvideMenuResource("Menus.ctmenu", 1)]
[Guid(PackageGuids.PackageString)]
[ProvideOptionPage(typeof(GeneralOptionPage), "SQLGuardian", "General", 0, 0, true)]
[ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
public sealed class SQLGuardianPackage : AsyncPackage
{
    public static SQLGuardianPackage? Instance { get; private set; }

    public ErrorListGateway ErrorList { get; } = new();

    private AnalyzeAfterExecuteHook? _afterExecuteHook;

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        Instance = this;

        await AnalyzeActiveDocumentCommand.InitializeAsync(this);
        await ClearErrorListCommand.InitializeAsync(this);
        _afterExecuteHook = await AnalyzeAfterExecuteHook.TryInitializeAsync(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Instance is not null)
        {
            JoinableTaskFactory.Run(async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                _afterExecuteHook?.Dispose();
                _afterExecuteHook = null;
                ErrorList.Dispose();
            });
        }

        base.Dispose(disposing);
    }
}
