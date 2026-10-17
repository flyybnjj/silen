using System.Windows.Input;

namespace Silen.App;

public sealed partial class MainViewModel
{
    private bool initializing, startupLoading, startupIdentityRead, pcDialogOpen;
    private TaskCompletionSource startupReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task StartupPresentationReady => startupReady.Task;
    internal bool StartupCancelled { get; private set; }
    internal void CancelStartup()
    {
        StartupCancelled = true;
        if (CancelCommand.CanExecute(null)) CancelCommand.Execute(null);
    }
    public BloomActivityViewModel Activity { get; } = new();
    public bool ShowStartupLoading => startupLoading;
    public bool IsManualDialogOpen => Settings.ShowManual;
    public bool IsPcDialogOpen => pcDialogOpen;
    public bool IsOptimizationDialogOpen => IsManualDialogOpen || pcDialogOpen;
    public string OptimizationDialogTitle => IsManualDialogOpen ? "Optimizador manual" : "Optimizar PC";
    public ICommand CloseOptimizationDialogCommand { get; }
    public ICommand DismissActivityCommand { get; }
    internal void BeginStartupPresentation()
    {
        startupLoading = true; startupIdentityRead = false; Changed(nameof(ShowStartupLoading));
        startupReady = new(TaskCreationOptions.RunContinuationsAsynchronously); StartupCancelled = false;
    }
    private void EndStartupPresentation()
    {
        if (!startupLoading) return;
        startupLoading = false; Changed(nameof(ShowStartupLoading));
        startupReady.TrySetResult();
    }
    private void OpenPcDialog()
    {
        Settings.CloseManual(); pcDialogOpen = true;
        Changed(nameof(IsPcDialogOpen)); Changed(nameof(IsOptimizationDialogOpen)); Changed(nameof(OptimizationDialogTitle));
    }
    private void CloseOptimizationDialog()
    {
        var wasManual = IsManualDialogOpen;
        Settings.CloseManual(); pcDialogOpen = false;
        Changed(nameof(IsPcDialogOpen)); Changed(nameof(IsOptimizationDialogOpen));
        Activity.Dismiss();
        SectionNavigationRequested?.Invoke(wasManual ? "ManualOptimizerLauncher" : "PcOptimizationSection");
    }
}
