using System.IO;
using System.Windows.Threading;
using Silen.Core;
using Silen.Windows;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace Silen.App;


internal static class InteractionSmokeChecks
{
    public static async Task<IReadOnlyList<string>> RunAsync(string directory, Dispatcher dispatcher)
    {
        var checks = new List<string>();
        await RunEarlyScanChecksAsync(directory, dispatcher, checks);
        await RunRemainingViewChecksAsync(directory, checks);
        await RunUnfocusedTableSelectionChecksAsync(directory, checks);
        await RunSummaryRecoveryChecksAsync(directory, checks);
        await RunSummaryErrorChecksAsync(directory, checks);
        await RunSafetySelectionChecksAsync(directory, checks);
        await RunRecoverySelectionChecksAsync(directory, dispatcher, checks);
        await RunAutomaticStartupChecksAsync(directory, checks);
        await RunPcOptimizationChecksAsync(directory, checks);
        await RunUpdateGamingCleanupChecksAsync(directory, checks);
        await RunBloomChecksAsync(directory, checks);
        await RunRamNetworkProductChecksAsync(directory, checks);
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Fallo de interacción: " + name);
            checks.Add(name);
        }

        var sample = new MetricSample { CpuPercent = 32, MemoryPercent = 61, GpuPercent = null };
        var fixture = new SystemSnapshot
        {
            Machine = new() { Manufacturer = "Lenovo", Model = "FixtureModel", ProductName = "Fixture product", DeviceKind = DeviceKind.Laptop, SystemSku = "FixtureSku" },
            Samples = [sample], Drivers = [new() { DeviceName = "Fixture graphics", Provider = "Fixture" }],
            Processes = [new("Fixture game", 54321, 300)]
        };
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serialRequests = new List<bool>();
        var opened = new List<ActionKind>();
        var offline = false;
        var cancelScan = true;
        var native = new FixtureOptimizationService();
        var fortnite = new FixtureFortniteProfileService(directory);
        var approveOptimization = false;
        var optimizationPrompts = 0;
        string? capturedExecutable = null;
        var denyCapture = false;
        var elevationRequests = 0;
        var elevationCancelled = true;
        var elevatedWindowsOpened = 0;
        var services = new ViewModelServices
        {
            ConfirmAction = (_, _) => true,
            CreateNativeTools = () => native,
            CreateFortniteProfile = () => fortnite,
            ConfirmOptimization = () => { optimizationPrompts++; return approveOptimization; },
            ChooseFortniteFolder = () => Path.Combine(directory, "Manually selected Fortnite fixture"),
            ResolvePresentMon = () => Environment.ProcessPath!,
            CaptureDirectory = Path.Combine(directory, "fixture-captures"),
            IsElevated = () => false,
            OpenElevated = () =>
            {
                elevationRequests++;
                if (elevationCancelled) throw new System.ComponentModel.Win32Exception(1223, "Fixture UAC cancelled");
                elevatedWindowsOpened++;
            },
            Capture = (executable, _, _, output, _) =>
            {
                if (denyCapture) throw new InvalidOperationException("Fixture trace session: access denied; administrative privileges required.");
                capturedExecutable = executable;
                return Task.FromResult(new GamingSession { ProcessName = "Fixture game", SourceFile = output, FrameCount = 30, TimingColumn = "FrameTime", AverageFps = 60, AverageFrameTimeMs = 16.667, P95FrameTimeMs = 17, P99FrameTimeMs = 18, OnePercentLowFps = 55, FrameTimesMs = [16, 17, 18] });
            },
            Collect = async (_, token, detailed) =>
            {
                if (cancelScan)
                {
                    detailed?.Report(new("Fixture stage", "Fixture observation", 1, 4, sample));
                    started.TrySetResult();
                    await release.Task.WaitAsync(token);
                }
                token.ThrowIfCancellationRequested(); return fixture;
            },
            FindUpdates = _ => Task.FromResult(offline ? new DriverUpdateResult(false, [], "Fixture offline") : new DriverUpdateResult(true, [new("Fixture Windows offer", "Fixture", "Display", "2", "2026-10-14")], "Fixture offers")),
            FindOem = (_, serial, _) =>
            {
                serialRequests.Add(serial);
                return Task.FromResult(offline
                    ? new OemSupportResult(false, "Lenovo", "Fixture product", "https://support.lenovo.com/", [], "Fixture offline; reintenta o abre el portal.", false)
                    : new OemSupportResult(true, "Lenovo", "Fixture product", "https://support.lenovo.com/", [new("Fixture chipset", "1", "Chipset", "2026-10-14", "Windows 11", "https://download.lenovo.com/fixture.exe", "https://support.lenovo.com/")], "Fixture catalog", true, serial ? "••••1234" : ""));
            },
            Open = (kind, _) => opened.Add(kind)
        };
        using var model = new MainViewModel(Path.Combine(directory, "fixture-history"), services);
        var tools = model.Tools;
        var simpleFortnite = new FixtureFortniteProfileService(Path.Combine(directory, "simple"));
        using var simpleModel = new MainViewModel(Path.Combine(directory, "simple-reports"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), CreateFortniteProfile = () => simpleFortnite,
            ConfirmOptimization = () => { optimizationPrompts++; return approveOptimization; },
            ResolvePresentMon = () => Environment.ProcessPath!, IsElevated = () => false,
            Open = (_, _) => throw new InvalidOperationException("Simple optimization must stay in Silen")
        });
        var optimizeCommand = simpleModel.Fortnite.GetType().GetProperty("OptimizeCommand")?.GetValue(simpleModel.Fortnite) as ICommand;
        Check(optimizeCommand is not null && optimizeCommand.CanExecute(null), "Simple optimization button is available without exposing a preview or selection form");
        await ExecuteAsync(optimizeCommand!);
        Check(optimizationPrompts == 1 && simpleFortnite.ApplyCalls == 0 && !simpleModel.Fortnite.HasPreview, "Declining optimization makes no changes and does not prepare a hidden profile");
        approveOptimization = true;
        await ExecuteAsync(optimizeCommand!);
        Check(optimizationPrompts == 2 && simpleFortnite.AppliedIds.SequenceEqual(["winutil-game-mode", "fortnite-settings"]) && simpleModel.Fortnite.Status == "Ajustes aplicados." && !simpleModel.Fortnite.HasActionError, "Confirmed optimization prepares applicable defaults automatically and reports ready after verified completion");
        simpleFortnite.PartialResultNext = true;
        await ExecuteAsync(optimizeCommand!);
        Check(simpleModel.Fortnite.HasActionError && simpleModel.Fortnite.Status != "Ajustes aplicados.", "A partial failure never reports PC ready in the simple flow");
        Check(simpleModel.Fortnite.HasFailedSteps && simpleModel.Fortnite.FailedSteps.All(step => !step.Success && !string.IsNullOrWhiteSpace(step.Message)), "Partial optimization exposes failed step messages for the error disclosure");
        var selectionCalls = simpleFortnite.ApplyCalls;
        approveOptimization = false;
        await ExecuteAsync(simpleModel.Fortnite.ApplySelectionCommand);
        Check(simpleFortnite.ApplyCalls == selectionCalls, "Declining manual selection makes no system changes");
        approveOptimization = true;
        simpleModel.Fortnite.Options.Single(option => option.Option.Id == "winutil-game-mode").Selected = false;
        await ExecuteAsync(simpleModel.Fortnite.ApplySelectionCommand);
        Check(simpleFortnite.AppliedIds.SequenceEqual(["fortnite-settings"]) && !simpleModel.Fortnite.HasActionError, "Confirmed manual selection applies only the chosen available controls");
        var updateMode = 0;
        var cancelDownload = false;
        var offeredDriver = new DriverUpdateInfo("Driver disponible", "Fixture", "Display", "", "2026-10-14", "12345678-1234-1234-1234-123456789abc", 1);
        using var updateModel = new MainViewModel(Path.Combine(directory, "updates-flow"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), CreateFortniteProfile = () => simpleFortnite,
            ResolvePresentMon = () => Environment.ProcessPath!,
            IsElevated = () => true,
            FindUpdates = _ => Task.FromResult(updateMode == 0 ? new DriverUpdateResult(true, [], "Fixture empty") : updateMode == 1 ? new DriverUpdateResult(true, [offeredDriver], "Fixture offers") : new DriverUpdateResult(false, [offeredDriver], "Fixture incomplete")),
            DownloadDriverUpdates = async (updates, token) =>
            {
                if (cancelDownload) { await Task.Delay(Timeout.Infinite, token); }
                return new(true, updates.Select(update => update.UpdateId).ToList(), "Descarga lista; no se instalaron drivers.");
            }
        });
        var updateWindow = new MainWindow(updateModel) { Width = 1240, Height = 820, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        updateModel.SelectedPage = 2; updateWindow.Show();
        await updateModel.SearchUpdatesAsync();
        Check(!updateModel.HasUpdates && !updateModel.DownloadUpdatesCommand.CanExecute(null) && updateModel.UpdateStatus.Contains("No hay actualizaciones"), "No-update search shows a clear empty result and no downloadable actions");
        await RenderFixtureWindowAsync(updateWindow, Path.Combine(directory, "updates-none.png"));
        updateMode = 1;
        await updateModel.SearchUpdatesAsync();
        Check(updateModel.HasUpdates && updateModel.HasDownloadableUpdates && updateModel.DownloadUpdatesCommand.CanExecute(null), "Verified offers expose download and available-update list");
        ((Expander)updateWindow.FindName("AvailableUpdates")).IsExpanded = true;
        await RenderFixtureWindowAsync(updateWindow, Path.Combine(directory, "updates-available.png"));
        await updateModel.DownloadUpdatesAsync();
        Check(updateModel.Updates.Single().Downloaded && !updateModel.HasDownloadableUpdates && !updateModel.DownloadUpdatesCommand.CanExecute(null) && updateModel.DownloadUpdatesLabel == "Descargadas", "Download completion marks only confirmed payloads downloaded and disables repeat download");
        await RenderFixtureWindowAsync(updateWindow, Path.Combine(directory, "updates-downloaded.png"));
        await updateModel.SearchUpdatesAsync();
        cancelDownload = true;
        var pendingDownload = updateModel.DownloadUpdatesAsync();
        Check(updateModel.IsBusy && !updateModel.ScanCommand.CanExecute(null) && updateModel.CancelCommand.CanExecute(null), "Driver download shares operation gating and cancellation");
        updateModel.CancelCommand.Execute(null);
        await pendingDownload;
        Check(!updateModel.Updates.Single().Downloaded && updateModel.UpdateStatus.Contains("cancelada") && updateModel.DownloadUpdatesCommand.CanExecute(null), "Cancelled download keeps offers retryable without claiming downloaded");
        updateMode = 2;
        await updateModel.SearchUpdatesAsync();
        Check(!updateModel.HasUpdates && !updateModel.HasDownloadableUpdates && updateModel.HasError && updateModel.UpdateStatus.Contains("no disponible"), "Incomplete query does not expose unverified downloads or claim there are no updates");
        updateWindow.Hide(); updateWindow.Close();
        var permitElevation = false;
        using var permissionModel = new MainViewModel(Path.Combine(directory, "update-permission"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), CreateFortniteProfile = () => simpleFortnite,
            ResolvePresentMon = () => Environment.ProcessPath!, IsElevated = () => false,
            FindUpdates = _ => Task.FromResult(new DriverUpdateResult(true, [offeredDriver], "Fixture offer")),
            DownloadDriverUpdates = (_, _) => throw new InvalidOperationException("Downloads must wait for elevation"),
            OpenElevatedForUpdates = () => { if (!permitElevation) throw new System.ComponentModel.Win32Exception(1223); }
        });
        await permissionModel.SearchUpdatesAsync();
        await permissionModel.DownloadUpdatesAsync();
        Check(permissionModel.DownloadNeedsPermission && permissionModel.UpdateStatus.Contains("Permiso cancelado") && permissionModel.DownloadUpdatesCommand.CanExecute(null), "Cancelled download permission keeps a clear recovery and does not start a download");
        permitElevation = true;
        await permissionModel.DownloadUpdatesAsync();
        var driverStart = Elevation.CreateStartInfo();
        Check(driverStart.ArgumentList.Count == 0 && driverStart.Verb == "runas" && driverStart.WindowStyle == ProcessWindowStyle.Hidden && driverStart.FileName == Path.Combine(AppContext.BaseDirectory, "Silen.exe"), "La elevación prepara únicamente el ejecutor oculto; no hay argumentos para abrir una interfaz administrativa");
        Check(!model.Fortnite.HasPreview && !model.Fortnite.ApplyCommand.CanExecute(null) && fortnite.ApplyCalls == 0, "Fortnite optimization requires reviewing detected installation and selected changes before any mutation");
        await model.Fortnite.DetectAsync();
        Check(model.Fortnite.HasPreview && model.Fortnite.IsInstalled && fortnite.ApplyCalls == 0 && model.Fortnite.Options.Count == 4 && !model.Fortnite.Options.Single(o => o.Option.Id == "gpu-preference").Selected && !model.Fortnite.Options.Single(o => o.Option.Id == "animations").Selected, "Fortnite preview detects paths without mutating and omits unavailable or optional changes from selection");
        model.Fortnite.Options.Single(o => o.Option.Id == "winutil-game-mode").Selected = false;
        await model.Fortnite.ApplyAsync();
        Check(fortnite.AppliedIds.SequenceEqual(["fortnite-settings"]) && model.Fortnite.Steps.Single().Changed && model.Fortnite.HasChanges && opened.Count == 0, "Optimize PC dispatches only selected available Fortnite steps and presents actual results with restore history");
        model.Fortnite.SelectedChange = model.Fortnite.Changes.Single();
        await model.Fortnite.UndoAsync();
        Check(fortnite.RestoredIds.SequenceEqual(["fixture-fortnite-change"]) && !model.Fortnite.HasChanges, "Fortnite restore dispatches selected saved change and refreshes history");
        fortnite.PartialCancelNext = true;
        await model.Fortnite.ApplyAsync();
        Check(model.Fortnite.HasChanges && model.Fortnite.HasActionError && model.Fortnite.Status.Contains("cancelado") && fortnite.PreviewTokens[^1] == CancellationToken.None, "A partially cancelled Fortnite profile reloads saved backups without the cancelled token");
        fortnite.FailPreviewNext = true;
        await model.Fortnite.DetectAsync();
        Check(!model.Fortnite.HasPreview && model.Fortnite.Options.Count == 0 && model.Fortnite.HasActionError && !model.Fortnite.ApplyCommand.CanExecute(null), "A failed replacement Fortnite preview disables stale selections and requires detecting again");
        await ExecuteAsync(model.Fortnite.ChooseFolderCommand);
        Check(model.Fortnite.IsInstalled && model.Fortnite.InstallPath.EndsWith("Manually selected Fortnite fixture") && fortnite.LastRequestedDirectory == model.Fortnite.InstallPath, "Manual folder choice is validated through profile preview and displayed in the app");
        model.Fortnite.Options.Single(o => o.Option.Id == "gpu-preference").Selected = true;
        Check(!model.Fortnite.Options.Single(o => o.Option.Id == "gpu-preference").Selected, "Unavailable Fortnite option cannot be selected through programmatic or keyboard input");
        fortnite.PartialResultNext = true;
        fortnite.CancelResultNext = true;
        await model.Fortnite.ApplyAsync();
        Check(model.Fortnite.Steps.Count == 2 && model.Fortnite.Steps.Count(s => s.Changed) == 1 && model.Fortnite.Steps.Any(s => !s.Success) && model.Fortnite.HasActionError && fortnite.LastRequestedDirectory == model.Fortnite.InstallPath, "Mixed profile results preserve successful and skipped steps instead of claiming complete optimization");
        fortnite.MissingInstallation = true;
        var appliedBeforeMissing = fortnite.ApplyCalls;
        await model.Fortnite.DetectAsync();
        Check(!model.Fortnite.IsInstalled && !model.Fortnite.Options.Single(o => o.Option.Id == "fortnite-settings").Option.Available && fortnite.ApplyCalls == appliedBeforeMissing, "Missing Fortnite disables game configuration and keeps preview read-only");
        fortnite.MissingInstallation = false;
        await model.Fortnite.DetectAsync();
        Check(tools.RefreshCommand.CanExecute(null) && !tools.ApplyPowerCommand.CanExecute(null) && !tools.UndoCommand.CanExecute(null), "Native improvement inspection is available before a full scan");
        await tools.RefreshAsync();
        Check(native.Mutations == 0 && !model.HasSnapshot && tools.HasInspection && tools.GamingToggles.Count == 2 && tools.ImprovementToggles.Single().State == "Activado", "Inspecting concrete settings needs no scan and makes no mutations");
        async Task ExecuteAsync(ICommand command, object? parameter = null)
        {
            Check(command.CanExecute(parameter), "Tool command is available for selected target");
            command.Execute(parameter);
            while (model.IsBusy) await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            if (command == tools.EnableToggleCommand || command == tools.DisableToggleCommand || command == tools.ApplyPowerCommand || command == tools.ToggleStartupCommand)
                await tools.ApplyManualAsync();
        }
        Check(model.HasBundledCollector && model.CaptureCommand.CanExecute(null), "Included collector enables capture without executable selection");
        model.ProcessId = Environment.ProcessId.ToString();
        await model.CaptureAsync();
        Check(model.HasSessionA && capturedExecutable == Environment.ProcessPath && model.SessionA!.Session.FrameCount == 30 && model.SessionA.Source.EndsWith(".csv"), "Capture uses resolved included collector and presents measured result");
        denyCapture = true;
        await model.CaptureAsync();
        Check(model.OpenElevatedCommand.CanExecute(null) && model.CanRequestCaptureElevation && elevationRequests == 0 && model.HasCaptureError && !model.HasError, "ETW permission failure offers explicit elevation without launching it automatically");
        await ExecuteAsync(model.OpenElevatedCommand);
        Check(elevationRequests == 1 && elevatedWindowsOpened == 0 && model.CaptureError.Contains("Cancelaste") && model.OpenElevatedCommand.CanExecute(null) && model.Status.Contains("cancelada"), "Cancelled UAC request gives inline recovery and allows an explicit retry");
        elevationCancelled = false;
        await ExecuteAsync(model.OpenElevatedCommand);
        Check(elevationRequests == 2 && elevatedWindowsOpened == 1 && !model.CanRequestCaptureElevation && model.HasSessionA && !model.IsClosing && model.Status.Contains("Esta sesión sigue disponible"), "Explicit elevation launches a new instance and keeps original observation session available");
        var elevatedStart = Elevation.CreateStartInfo();
        Check(elevatedStart.FileName == Path.Combine(AppContext.BaseDirectory, "Silen.exe") && elevatedStart.WorkingDirectory == AppContext.BaseDirectory && elevatedStart.UseShellExecute && elevatedStart.Verb == "runas" && elevatedStart.ArgumentList.Count == 0 && elevatedStart.Arguments == "" && elevatedStart.WindowStyle == System.Diagnostics.ProcessWindowStyle.Hidden, "Elevation launch targets only this apphost with native UAC and no smoke or external-tool arguments");
        denyCapture = false;
        using (var elevatedModel = new MainViewModel(Path.Combine(directory, "already-elevated"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), ResolvePresentMon = () => Environment.ProcessPath!,
            IsElevated = () => true, OpenElevated = () => throw new InvalidOperationException("Fixture must never elevate an elevated process"),
            CaptureDirectory = Path.Combine(directory, "elevated-fixture-captures"),
            Capture = (_, _, _, _, _) => throw new InvalidOperationException("Fixture access denied; administrative privileges required")
        }))
        {
            elevatedModel.ProcessId = Environment.ProcessId.ToString();
            await elevatedModel.CaptureAsync();
            Check(elevatedModel.HasCaptureError && !elevatedModel.CanRequestCaptureElevation && !elevatedModel.OpenElevatedCommand.CanExecute(null), "An already elevated process keeps permission error without offering another elevation");
        }
        Check(model.ScanCommand.CanExecute(null) && !model.ExportCommand.CanExecute(null) && !model.SearchOemCommand.CanExecute(null), "Empty-state commands explain missing observation");
        model.NavigateCommand.Execute("4");
        Check(model.SelectedPage == 4, "Navigation command selects Historial after removing Gaming");
        var cancelled = model.ScanAsync();
        await started.Task;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        Check(model.IsScanning && model.ScanPercent == 25 && model.CpuValue == "32%" && model.GpuValue == "No disponible" && model.CpuTrace.Count == 1, "Completed-step progress and actual live sample with unavailable GPU");
        Check(!model.ScanCommand.CanExecute(null) && model.CancelCommand.CanExecute(null), "Concurrent scan blocked and cancel available");
        model.CancelCommand.Execute(null);
        Check(!await cancelled && !model.IsBusy && !model.IsScanning && !model.HasSnapshot && serialRequests.Count == 0 && model.Status.Contains("parciales"), "Cancelled scan preserves empty state and saves no partial observation");
        cancelScan = false;
        Check(await model.ScanAsync() && serialRequests.SequenceEqual([false]) && model.HasOemPackages && model.CanSearchSerial, "Fresh scan automatically finds model without sending serial");
        Check(model.DeviceKindLabel == "Portátil" && model.DeviceIdentity.Contains("FixtureSku"), "Device type, model and SKU displayed");
        await ExecuteAsync(tools.DisableToggleCommand, tools.ImprovementToggles.Single());
        Check(tools.ImprovementToggles.Single().State == "Desactivado" && tools.HasChanges, "Applying animations refreshes actual state and exposes reversible change");
        tools.SelectedChange = tools.Changes.Single();
        await ExecuteAsync(tools.UndoCommand);
        Check(tools.ImprovementToggles.Single().State == "Activado" && !tools.HasChanges, "Undo restores previous state and refreshes change list");
        await ExecuteAsync(tools.EnableToggleCommand, tools.GamingToggles.Single(t => t.Setting.Id == "game-mode"));
        await ExecuteAsync(tools.DisableToggleCommand, tools.GamingToggles.Single(t => t.Setting.Id == "xbox-captures"));
        Check(tools.GamingToggles.Single(t => t.Setting.Id == "game-mode").Setting.Enabled == true && tools.GamingToggles.Single(t => t.Setting.Id == "xbox-captures").Setting.Enabled == false, "Gaming controls dispatch separate Game Mode and Xbox capture changes");
        tools.SelectedPowerScheme = tools.PowerSchemes.Single(p => p.Id == "performance");
        await ExecuteAsync(tools.ApplyPowerCommand);
        Check(tools.PowerSchemes.Single(p => p.Id == "performance").Active && !tools.ApplyPowerCommand.CanExecute(null), "Applying selected existing power plan refreshes active state");
        var powerControl = new ContentControl { Content = tools, ContentTemplate = (DataTemplate)Application.Current.FindResource("PowerSchemeTemplate") };
        powerControl.Measure(new Size(700, 240)); powerControl.Arrange(new Rect(0, 0, 700, 240)); powerControl.UpdateLayout();
        Check(VisibleText(powerControl).Contains("Rendimiento fixture"), "Power selector displays selected plan name rather than object or GUID");
        tools.SelectedStartup = tools.StartupEntries.Single();
        await ExecuteAsync(tools.ToggleStartupCommand);
        Check(!tools.StartupEntries.Single().Entry.Enabled && tools.StartupEntries.Single().State == "Retirada de Run" && tools.StartupActionLabel == "Añadir restauración de inicio", "Startup action removes only selected Run entry and offers restoring it");
        await ExecuteAsync(tools.PreviewTempCommand);
        Check(tools.TemporaryFiles.Count == 2 && !tools.CleanTempCommand.CanExecute(null) && tools.TemporaryFiles.All(f => !f.Selected), "Cleanup previews files without deleting or selecting them automatically");
        tools.TemporaryFiles[0].Selected = true;
        native.CancelPreviewNext = true;
        await ExecuteAsync(tools.PreviewTempCommand);
        Check(!tools.HasPreview && !tools.HasTemporaryFiles && !tools.CleanTempCommand.CanExecute(null) && tools.CleanupStatus.Contains("cancelada"), "Cancelled replacement preview clears stale file IDs selection and cleanup command");
        await ExecuteAsync(tools.PreviewTempCommand);
        tools.TemporaryFiles[1].Selected = true;
        await ExecuteAsync(tools.CleanTempCommand);
        Check(native.CleanedIds.SequenceEqual(["second"]) && !tools.HasTemporaryFiles && tools.CleanupStatus.Contains("2,0 KiB"), "Cleanup sends only selected file IDs and shows recovered bytes with a fresh preview required");
        var countBeforeFailure = tools.Changes.Count;
        native.FailNext = true;
        await ExecuteAsync(tools.EnableToggleCommand, tools.ImprovementToggles.Single() with { Setting = tools.ImprovementToggles.Single().Setting with { Enabled = null } });
        Check(tools.Status.Contains("Fixture permission denied") && tools.HasActionError && tools.Changes.Count == countBeforeFailure && model.Status.Contains("Fixture permission denied"), "Rejected change reports backend reason distinctly and footer finishes without a successful change");
        foreach (var pending in tools.PendingManual.ToArray()) tools.RemoveManualCommand.Execute(pending);
        native.PartialCancelNext = true;
        await ExecuteAsync(tools.EnableToggleCommand, tools.GamingToggles.Single(t => t.Setting.Id == "xbox-captures"));
        Check(tools.GamingToggles.Single(t => t.Setting.Id == "xbox-captures").Setting.Enabled == true && tools.Changes.Count == countBeforeFailure + 1 && tools.Status.Contains("cancelada"), "Cancelled partial change refreshes current state and saved undo record before releasing operation");
        model.DriverSearch = "no such device";
        Check(!model.DriversView.Cast<object>().Any(), "Driver inventory filter");
        model.OemSearch = "chipset";
        Check(model.OemPackagesView.Cast<object>().Count() == 1, "Official catalog category filter");
        model.SelectedOemPackage = model.OemPackages.Single();
        await model.OpenOemAsync(true);
        Check(opened.SequenceEqual([ActionKind.OpenManufacturer]) && model.Status.Contains("compatibilidad"), "Official package command opens reviewed URL without installing");
        opened.Clear();
        model.OemSearch = "absent";
        Check(!model.OemPackagesView.Cast<object>().Any(), "Official catalog unmatched filter");
        model.SelectedProcess = model.CaptureProcesses.Single();
        Check(model.ProcessId == "54321", "Real process choice supplies PID");
        model.ProcessId = "42";
        Check(model.SelectedProcess is null && model.ProcessId == "42", "Manual PID clears old picker selection");
        model.ProcessId = "0";
        Check(!model.ValidateCaptureInputs(out _, out _) && model.HasCaptureError && !model.HasError && !model.CanRequestCaptureElevation && !model.OpenElevatedCommand.CanExecute(null), "Invalid capture input stays beside capture form without offering elevation");
        await model.SearchOemAsync(true);
        Check(serialRequests.SequenceEqual([false, true]) && model.SerialDisplay.Contains("1234"), "Only explicit serial action sends serial request");
        await model.SearchUpdatesAsync();
        Check(model.HasUpdates, "Windows Update result stays separate from model catalog");
        model.SelectedProcess = model.CaptureProcesses.Single();
        var archived = fixture with
        {
            CapturedAt = DateTimeOffset.Now.AddDays(-30),
            Processes = [new("Archived name with reused PID", 54321, 100), new("Another archived process", 999, 50)]
        };
        model.ApplySnapshot(archived);
        Check(model.CaptureProcesses.Count == 0 && !model.HasCaptureProcesses && model.SelectedProcess is null && model.ProcessId == "", "History clears capture candidates selected process and stale PID even when archived PID matches current selection");
        Check(model.Snapshot!.Processes.Count == 2, "Archived process inventory remains available for historical inspection");
        model.ProcessId = "42";
        Check(model.ProcessId == "42" && model.SelectedProcess is null && model.RefreshProcessesCommand.CanExecute(null), "Manual PID and live refresh remain available after history load");
        Check(!model.HasUpdates && !model.HasOemPackages && !model.HasOemPortal && !model.CanSearchSerial && model.SelectedOemPackage is null, "Loading observation resets stale updates catalog selection and serial permission");
        Check(!await model.SearchOemAsync(true) && serialRequests.Count == 2, "History cannot send current machine serial");
        offline = true;
        await model.SearchOemAsync(false);
        Check(!model.HasOemPackages && model.HasOemPortal && model.OemStatus.Contains("offline") && model.SearchOemCommand.CanExecute(null), "Offline OEM keeps official portal and retry available");
        await model.SearchUpdatesAsync();
        Check(!model.HasUpdates && model.UpdateStatus.Contains("no disponible") && model.HasError, "Unavailable update query reports useful result");
        model.ClearErrorCommand.Execute(null);
        Check(!model.HasError, "Dismiss error command");
        model.ApplySnapshot(fixture, isCurrent: true);
        var guidesBeforeStartup = model.Actions.Count;
        var integratedSections = new List<string>();
        model.SectionNavigationRequested += integratedSections.Add;
        await model.OpenGuideAsync("OpenStartup");
        Check(model.SelectedPage == 0 && opened.Count == 0 && model.Actions.Count == guidesBeforeStartup, "Revisar inicio abre el optimizador principal sin herramientas externas");
        Check(integratedSections.SequenceEqual(["PcOptimizationSection"]) && model.PcOptimization.HasPreview, "Revisar inicio prepara la selección principal sin activar el modo experto");
        var startupFinding = new FindingRow(new("fixture-startup", "Revisar inicio", "Fixture", "Fixture", Severity.Info, "Inicio", ActionKind.OpenStartup));
        model.SelectedPage = 0;
        await ExecuteAsync(model.OpenFindingCommand, startupFinding);
        Check(model.SelectedPage == 0 && integratedSections.Count == 2 && integratedSections[^1] == "PcOptimizationSection" && opened.Count == 0 && model.Actions.Count == guidesBeforeStartup, "La recomendación de inicio abre la misma selección principal sin acción externa");
        var guideKinds = Enum.GetValues<ActionKind>().Where(k => k is not (ActionKind.None or ActionKind.OpenManufacturer)).ToList();
        foreach (var kind in guideKinds)
            await model.OpenGuideAsync(kind.ToString());
        var externalGuides = new[] { ActionKind.OpenWindowsUpdate, ActionKind.OpenOptionalDrivers, ActionKind.OpenGraphics, ActionKind.OpenTaskManager };
        Check(opened.SequenceEqual(externalGuides) && model.Actions.Count == externalGuides.Length + 1 && model.Actions.All(a => a.Outcome.Contains("no se comprobó ni aplicó")), "Only explicit Windows capabilities without integrated controls dispatch and log external opened-only outcome");
        model.ProcessId = Environment.ProcessId.ToString(); denyCapture = true;
        await model.CaptureAsync(); denyCapture = false;
        native.BlockInspection = true;
        var pendingTools = tools.RefreshAsync();
        await native.InspectionStarted.Task;
        Check(model.IsBusy && !model.ScanCommand.CanExecute(null) && !model.CaptureCommand.CanExecute(null) && !tools.PreviewTempCommand.CanExecute(null) && !model.OpenElevatedCommand.CanExecute(null) && model.CancelCommand.CanExecute(null), "Native operation shares scan capture cleanup elevation and cancel gating");
        var closing = model.PrepareToCloseAsync();
        await native.InspectionCancelled.Task;
        model.OpenElevatedCommand.Execute(null);
        Check(!closing.IsCompleted && !tools.RefreshCommand.CanExecute(null) && !model.OpenElevatedCommand.CanExecute(null) && elevationRequests == 2, "Closing awaits native cleanup and blocks new tool and elevation launches");
        native.ReleaseInspection.TrySetResult();
        await closing; await pendingTools;
        Check(model.IsClosing && !model.CanStartOperation && !model.ScanCommand.CanExecute(null), "Closing waits for cleanup and disables new work");
        using var unavailable = new MainViewModel(Path.Combine(directory, "missing-collector"), new ViewModelServices { CreateNativeTools = () => new FixtureOptimizationService(), ResolvePresentMon = () => throw new InvalidOperationException("Fixture bundled collector missing; extract Silen again.") });
        Check(!unavailable.HasBundledCollector && !unavailable.CaptureCommand.CanExecute(null) && unavailable.CollectorStatus.Contains("extract"), "Missing bundled collector blocks capture with package recovery instruction");
        using var navigationModel = new MainViewModel(Path.Combine(directory, "navigation-visual"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), ResolvePresentMon = () => Environment.ProcessPath!,
            Open = (_, _) => throw new InvalidOperationException("An integrated action must never launch another tool")
        });
        var navigationWindow = new MainWindow(navigationModel) { Width = 1240, Height = 820, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        navigationWindow.Show();
        await dispatcher.InvokeAsync(navigationWindow.UpdateLayout, DispatcherPriority.Loaded);
        Check(navigationWindow.Icon is not null, "Packaged window has the Silen icon");
        navigationModel.SelectedPage = 4;
        await navigationModel.WaitForFortniteDetectionAsync();
        await dispatcher.InvokeAsync(navigationWindow.UpdateLayout, DispatcherPriority.Loaded);
        Check(!navigationWindow.Pages.Items.OfType<TabItem>().Any(item => item.Header?.ToString() == "Gaming"), "La navegación no mantiene una pestaña Gaming duplicada");
        navigationModel.SelectedPage = 2;
        await dispatcher.InvokeAsync(navigationWindow.UpdateLayout, DispatcherPriority.Loaded);
        Check(VisibleText(navigationWindow, true).Contains("Drivers para tu equipo") && !VisibleText(navigationWindow, true).Contains("Inventario de drivers"), "Unscanned driver page shows discovery instead of empty inventory");
        navigationModel.ApplySnapshot(fixture, isCurrent: true);
        await navigationModel.OpenGuideAsync("OpenStartup");
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        navigationWindow.UpdateLayout();
        Check(navigationModel.SelectedPage == 0 && navigationModel.PcOptimization.HasPreview && navigationWindow.FindName("PcOptimizationSection") is FrameworkElement { IsVisible: true }, "La guía de inicio muestra el optimizador principal y conserva el menú experto cerrado");
        navigationWindow.Hide();
        var closingProfile = new FixtureFortniteProfileService(directory) { BlockApply = true, BlockRecovery = true };
        using var closingProfileModel = new MainViewModel(Path.Combine(directory, "profile-closing"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), CreateFortniteProfile = () => closingProfile,
            ResolvePresentMon = () => Environment.ProcessPath!, Open = (_, _) => throw new InvalidOperationException("Profile cannot open external tools")
        });
        await closingProfileModel.Fortnite.DetectAsync();
        var applyingProfile = closingProfileModel.Fortnite.ApplyAsync();
        await closingProfile.ApplyStarted.Task;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        Check(closingProfileModel.IsBusy && closingProfileModel.Fortnite.IsApplying && closingProfileModel.Fortnite.ProgressPercent == 50 && !closingProfileModel.ScanCommand.CanExecute(null) && !closingProfileModel.Tools.RefreshCommand.CanExecute(null) && !closingProfileModel.Fortnite.ApplyCommand.CanExecute(null), "Fortnite application reports real completed steps while sharing scan and native-tool busy gating");
        var profileClose = closingProfileModel.PrepareToCloseAsync();
        await closingProfile.ApplyCancelled.Task; await closingProfile.RecoveryStarted.Task;
        Check(!profileClose.IsCompleted && !closingProfileModel.Fortnite.DetectCommand.CanExecute(null) && !closingProfileModel.OpenFortniteCommand.CanExecute(null), "Closing cancels Fortnite work and waits for partial-change recovery before releasing the app");
        closingProfile.ReleaseRecovery.TrySetResult();
        await applyingProfile; await profileClose;
        Check(closingProfileModel.IsClosing && closingProfileModel.Fortnite.HasChanges && closingProfile.PreviewTokens[^1] == CancellationToken.None && !closingProfileModel.Fortnite.UndoCommand.CanExecute(null), "Closing finishes profile recovery with backups visible and all new profile actions blocked");
        await CheckAppUpdatesAsync(directory, dispatcher, Check);
        return checks;
    }

    private static async Task CheckAppUpdatesAsync(string directory, Dispatcher dispatcher, Action<bool, string> check)
    {
        var updater = new FixtureAppUpdateService();
        var helperFails = true;
        var helperLaunched = false;
        var mainStillOpenAtLaunch = false;
        var operationsBlockedAtLaunch = false;
        var scanStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scanRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MainWindow? window = null;
        using var model = new MainViewModel(Path.Combine(directory, "app-update-history"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            CreateFortniteProfile = () => new FixtureFortniteProfileService(directory),
            ResolvePresentMon = () => Environment.ProcessPath!, IsElevated = () => false,
            AppUpdates = updater,
            FindOem = (_, _, _) => Task.FromResult(new OemSupportResult(false, "Fixture", "Fixture", "", [], "Fixture no model", false)),
            PrepareAppUpdate = update => new ProcessStartInfo("fixture-helper") { Arguments = update.ArchivePath },
            LaunchAppUpdate = start =>
            {
                if (helperFails) throw new InvalidOperationException("Fixture helper locked");
                helperLaunched = start.FileName == "fixture-helper";
                mainStillOpenAtLaunch = window?.IsVisible == true && !modelClosing();
                operationsBlockedAtLaunch = window?.DataContext is MainViewModel launchModel && !launchModel.ScanCommand.CanExecute(null) && !launchModel.Tools.RefreshCommand.CanExecute(null);
            },
            Collect = async (_, token, _) =>
            {
                scanStarted.TrySetResult(); await scanRelease.Task.WaitAsync(token);
                return new SystemSnapshot();
            }
        });
        bool modelClosing() => window?.DataContext is MainViewModel { IsClosing: true };
        window = new MainWindow(model) { Width = 1240, Height = 820, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        window.Show();
        check(!model.AppUpdate.IsWorking && !model.AppUpdate.HasError && !model.AppUpdate.IsReady && !string.IsNullOrWhiteSpace(model.AppUpdate.Status) && model.AppUpdate.InstalledVersion.Split('.').Length == 3 && !model.AppUpdate.RestartCommand.CanExecute(null), "App update starts idle with a status and installed version, without offering restart");
        await model.StartAppUpdateCheckAsync();
        check(!model.AppUpdate.IsReady && !model.AppUpdate.HasError && model.AppUpdate.Status.Contains("actualizada"), "Verified no-update result reports current version without offering restart");
        updater.Offer = new(new Version(9, 0, 0), "v9.0.0", "https://github.com/flyybnjj/flyyoptimizer/releases/download/v9.0.0/Silen-win-x64.zip", 100, new string('a', 64));
        updater.BlockDownload = true;
        var downloading = model.StartAppUpdateCheckAsync();
        await updater.DownloadStarted.Task;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        check(model.AppUpdate.IsDownloading && model.AppUpdate.ProgressPercent == 40 && model.AppUpdate.Status.Contains("9.0.0") && model.ScanCommand.CanExecute(null), "New app releases auto-download with progress while analysis remains available");
        await RenderFixtureWindowAsync(window, Path.Combine(directory, "app-update-downloading.png"));
        updater.ReleaseDownload.TrySetResult(); await downloading;
        check(model.AppUpdate.IsReady && model.AppUpdate.RestartCommand.CanExecute(null) && !helperLaunched, "Verified automatic download waits for an explicit restart request");
        check(VisibleText(window, true).Contains("Reiniciar y actualizar"), "Ready update exposes the explicit restart control in the actual sidebar");
        await RenderFixtureWindowAsync(window, Path.Combine(directory, "app-update-ready.png"));
        window.Width = window.MinWidth; window.Height = window.MinHeight;
        await RenderFixtureWindowAsync(window, Path.Combine(directory, "app-update-ready-minimum.png"));
        check(Visuals<Button>(window).Where(button => button.Command == model.AppUpdate.RestartCommand || button.Command == model.AppUpdate.CheckCommand).All(button => FitsWindow(window, button)), "Ready app update controls fit completely inside the minimum-size window");
        var scan = model.ScanAsync(); await scanStarted.Task;
        check(!model.AppUpdate.RestartCommand.CanExecute(null), "An active analysis disables app restart without losing the prepared update");
        model.AppUpdate.RestartCommand.Execute(null);
        check(!helperLaunched && !model.IsClosing, "Programmatic restart during analysis cannot launch a helper or close the app");
        scanRelease.TrySetResult(); await scan;
        model.AppUpdate.RestartCommand.Execute(null);
        check(model.AppUpdate.HasError && model.AppUpdate.IsReady && !model.IsClosing && window.IsEnabled && model.AppUpdate.RestartCommand.CanExecute(null), "Helper launch failure leaves the app open and the prepared update retryable");
        await RenderFixtureWindowAsync(window, Path.Combine(directory, "app-update-restart-error.png"));
        Visuals<Expander>(window).Single(expander => expander.Header?.ToString() == "Ver error" && expander.IsVisible).IsExpanded = true;
        await RenderFixtureWindowAsync(window, Path.Combine(directory, "app-update-restart-error-expanded.png"));
        var navigation = Visuals<ListBox>(window).First();
        check(navigation.Items.OfType<ListBoxItem>().Where(item => item.IsVisible).All(item => FitsWindow(window, item)) && Visuals<Button>(window).Where(button => button.Command == model.AppUpdate.RestartCommand || button.Command == model.AppUpdate.CheckCommand).All(button => FitsWindow(window, button)), "El error de actualización conserva navegación visible, acceso a Ajustes desde el logo y controles al tamaño mínimo");
        helperFails = false;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        model.AppUpdate.RestartCommand.Execute(null);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        check(helperLaunched && mainStillOpenAtLaunch && operationsBlockedAtLaunch && model.IsClosing && !model.CanStartOperation, "Explicit restart launches the staged helper before safe close and blocks further PC operations");

        var failing = new FixtureAppUpdateService { CheckFails = true };
        using var failureModel = AppUpdateFixtureModel(directory, "app-update-error", failing);
        await failureModel.StartAppUpdateCheckAsync();
        check(failureModel.AppUpdate.HasError && !failureModel.AppUpdate.IsReady && failureModel.AppUpdate.CheckCommand.CanExecute(null) && failureModel.ScanCommand.CanExecute(null), "Release query errors offer retry and leave PC analysis usable");
        failing.CheckFails = false; failing.Offer = updater.Offer; failing.DownloadFails = true;
        await failureModel.StartAppUpdateCheckAsync();
        check(failureModel.AppUpdate.HasError && !failureModel.AppUpdate.IsReady && !failureModel.AppUpdate.RestartCommand.CanExecute(null), "Failed app download never exposes an unverified package as ready");
        failing.DownloadFails = false; await failureModel.StartAppUpdateCheckAsync();
        check(failureModel.AppUpdate.IsReady && !failureModel.AppUpdate.HasError, "Retry recovers from download failure and only then enables restart");

        var revisionVersion = new Version(1, 1, 0, 1);
        var revisionUpdater = new FixtureAppUpdateService { Offer = new(revisionVersion, "v1.1.0.1", updater.Offer.AssetUrl, 100, new string('b', 64)) };
        using var revisionModel = AppUpdateFixtureModel(directory, "app-update-revision", revisionUpdater, revisionVersion);
        await revisionModel.StartAppUpdateCheckAsync();
        check(!revisionModel.AppUpdate.IsReady && !revisionModel.AppUpdate.HasError && revisionModel.AppUpdate.Status.Contains("actualizada"), "An installed four-part version does not download the same release again");
        check(revisionModel.AppUpdate.InstalledVersion == "1.1.0.1", "Installed nonzero revision remains visible instead of showing a different three-part version");

        var cancelling = new FixtureAppUpdateService { Offer = updater.Offer, BlockDownload = true, WaitForCancellationCleanup = true };
        using var cancelModel = AppUpdateFixtureModel(directory, "app-update-cancel", cancelling);
        var pendingDownload = cancelModel.StartAppUpdateCheckAsync(); await cancelling.DownloadStarted.Task;
        cancelModel.AppUpdate.CancelCommand.Execute(null); await cancelling.Cancelled.Task;
        check(!pendingDownload.IsCompleted && !cancelModel.AppUpdate.IsReady, "Cancelling app download waits for partial-file cleanup and never marks it ready");
        cancelling.ReleaseCleanup.TrySetResult(); await pendingDownload;
        check(!cancelModel.AppUpdate.HasError && !cancelModel.AppUpdate.IsReady && cancelModel.AppUpdate.CheckCommand.CanExecute(null) && cancelModel.AppUpdate.Status.Contains("cancelada"), "Cancelled app download offers a fresh check without a false error or ready state");

        var closing = new FixtureAppUpdateService { Offer = updater.Offer, BlockDownload = true, WaitForCancellationCleanup = true };
        using var closingModel = AppUpdateFixtureModel(directory, "app-update-close", closing);
        var closeDownload = closingModel.StartAppUpdateCheckAsync(); await closing.DownloadStarted.Task;
        var close = closingModel.PrepareToCloseAsync(); await closing.Cancelled.Task;
        check(!close.IsCompleted && closingModel.IsClosing && !closingModel.AppUpdate.CheckCommand.CanExecute(null) && !closingModel.AppUpdate.RestartCommand.CanExecute(null), "Closing cancels the separate app download and blocks retries until cleanup completes");
        closing.ReleaseCleanup.TrySetResult(); await closeDownload; await close;
        check(!closingModel.AppUpdate.IsReady && !closingModel.AppUpdate.IsWorking && !closingModel.CanStartOperation, "Safe close awaits app download cleanup without exposing a partial package");
    }

    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var item in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return item;
    }

    private static bool FitsWindow(Window window, FrameworkElement element)
    {
        if (!element.IsVisible || window.Content is not FrameworkElement content) return false;
        var bounds = element.TransformToAncestor(content).TransformBounds(new Rect(element.RenderSize));
        return bounds.Top >= 0 && bounds.Left >= 0 && bounds.Right <= content.ActualWidth && bounds.Bottom <= content.ActualHeight;
    }

    private static MainViewModel AppUpdateFixtureModel(string directory, string name, IAppUpdateService updater, Version? installedVersion = null) => new(Path.Combine(directory, name), new ViewModelServices
    {
        CreateNativeTools = () => new FixtureOptimizationService(), CreateFortniteProfile = () => new FixtureFortniteProfileService(directory),
        ResolvePresentMon = () => Environment.ProcessPath!, IsElevated = () => false, AppUpdates = updater,
        InstalledAppVersion = installedVersion ?? typeof(MainViewModel).Assembly.GetName().Version!,
        PrepareAppUpdate = _ => throw new InvalidOperationException("Unexpected fixture helper preparation"),
        LaunchAppUpdate = _ => throw new InvalidOperationException("Unexpected fixture helper launch")
    });

    private sealed class FixtureAppUpdateService : IAppUpdateService
    {
        public AppRelease? Offer { get; set; }
        public bool CheckFails { get; set; }
        public bool DownloadFails { get; set; }
        public bool BlockDownload { get; set; }
        public bool WaitForCancellationCleanup { get; set; }
        public TaskCompletionSource DownloadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDownload { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseCleanup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AppRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CheckFails) throw new InvalidOperationException("Fixture GitHub unavailable");
            var normalizedCurrent = new Version(currentVersion.Major, currentVersion.Minor, Math.Max(0, currentVersion.Build), Math.Max(0, currentVersion.Revision));
            return Task.FromResult(Offer is not null && Offer.Version > normalizedCurrent ? Offer : null);
        }
        public async Task<PreparedAppUpdate> DownloadAsync(AppRelease release, IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (DownloadFails) throw new InvalidOperationException("Fixture archive hash invalid");
            progress?.Report(new(40, 100)); DownloadStarted.TrySetResult();
            if (BlockDownload)
            {
                try { await ReleaseDownload.Task.WaitAsync(cancellationToken); }
                catch (OperationCanceledException)
                {
                    Cancelled.TrySetResult();
                    if (WaitForCancellationCleanup) await ReleaseCleanup.Task;
                    throw;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new(release, "fixture-verified.zip");
        }
    }

    private static IEnumerable<string> VisibleText(DependencyObject root, bool visibleOnly = false)
    {
        if (visibleOnly && root is UIElement { IsVisible: false }) yield break;
        if (root is TextBlock text) yield return text.Text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in VisibleText(VisualTreeHelper.GetChild(root, i), visibleOnly)) yield return child;
    }

    private static async Task RunUnfocusedTableSelectionChecksAsync(string directory, List<string> checks)
    {
        var focusTarget = new Button { Content = "Continuar", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 16, 0, 0) };
        var table = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow, Height = 136, ItemsSource = new[] { "Dispositivo seleccionado", "Otro dispositivo" } };
        table.Columns.Add(new DataGridTextColumn { Header = "Dispositivo", Binding = new System.Windows.Data.Binding("."), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        table.SelectedIndex = 0;
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Selección conservada al salir de la tabla", Margin = new Thickness(0, 0, 0, 16), FontWeight = FontWeights.SemiBold });
        panel.Children.Add(table); panel.Children.Add(focusTarget);
        var surface = new Border { Padding = new Thickness(24), Child = panel };
        surface.SetResourceReference(Border.BackgroundProperty, "Ink");
        var window = new Window { Title = "Silen · selección", Content = surface, Width = 760, Height = 380, ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show();
            foreach (var theme in new[] { "Claro", "Oscuro", "Alto contraste" })
            {
                if (theme == "Alto contraste") ThemeManager.ApplyContrastFixture(); else ThemeManager.Apply(theme);
                FocusManager.SetFocusedElement(window, focusTarget); window.UpdateLayout();
                var row = (DataGridRow)table.ItemContainerGenerator.ContainerFromIndex(0);
                var other = (DataGridRow)table.ItemContainerGenerator.ContainerFromIndex(1);
                var accent = (Brush)Application.Current.Resources["Accent"];
                if (row.IsKeyboardFocusWithin || !row.IsSelected || row.BorderThickness.Left != 1 ||
                    !ReferenceEquals(row.BorderBrush, accent) || other.BorderThickness.Left != 1 ||
                    !Visuals<Border>(row).Any(border => border.BorderThickness.Left == 1 && ReferenceEquals(border.BorderBrush, accent)))
                    throw new InvalidOperationException("La fila seleccionada sin foco debe conservar una marca brick de una unidad, también en alto contraste.");
                await RenderFixtureWindowAsync(window, Path.Combine(directory, $"tabla-seleccion-sin-foco-{theme.ToLowerInvariant().Replace(' ', '-')}.png"));
                checks.Add($"Tabla sin foco en {theme}: marca persistente de selección de una unidad, sin depender solo del relleno");
            }
        }
        finally { window.Hide(); window.Close(); ThemeManager.Apply("Claro"); }
    }

    private static async Task RunSummaryRecoveryChecksAsync(string directory, List<string> checks)
    {
        var native = new FixtureOptimizationService();
        var point = new FixtureRestorePointService();
        var fallback = false; var undo = false; var approveSelection = false; var invalidateUndo = false; var fallbackPrompts = 0;
        using var model = new MainViewModel(Path.Combine(directory, "summary-recovery"), new ViewModelServices
        {
            CreateNativeTools = () => native, CreateRestorePoint = () => point,
            ConfirmPcOptimization = _ => approveSelection,
            ConfirmRestoreFallback = result => { fallbackPrompts++; return fallback; },
            ConfirmAction = (_, _) => { if (invalidateUndo) native.HideBackups = true; return undo; }
        });
        model.ApplySnapshot(new SystemSnapshot(), isCurrent: true);
        await model.PcOptimization.PrepareAsync();
        await model.PcOptimization.ApplyAsync();
        if (point.Calls != 0 || native.Mutations != 0) throw new InvalidOperationException("Rechazar el lote no debe solicitar siquiera un punto de restauración.");
        approveSelection = true;
        point.Result = new(RestorePointState.Reused, 42, DateTimeOffset.UtcNow.AddHours(-1));
        await model.PcOptimization.ApplyAsync();
        if (native.Mutations != 0 || fallbackPrompts != 1 || !model.Recovery.PointStatus.Contains("reutilizó"))
            throw new InvalidOperationException("Reutilizar un punto exige decisión explícita antes de modificar Windows.");
        checks.Add("Resumen detiene el lote cuando Windows reutiliza un punto y el usuario no confirma continuar");
        foreach (var state in new[] { RestorePointState.Disabled, RestorePointState.Unavailable, RestorePointState.Failed })
        {
            point.Result = new(state); await model.PcOptimization.ApplyAsync();
            if (native.Mutations != 0 || !model.Recovery.PointStatus.Contains("No se inició el lote") || model.Recovery.PointStatus.Contains("protegido", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Un punto ausente o fallido debe detener el lote con estado literal, sin declarar protección.");
            checks.Add($"Punto {state}: lote detenido, mensaje literal y sin mutaciones antes de consentimiento");
        }
        var beforePrompts = fallbackPrompts;
        point.Result = new(RestorePointState.Cancelled); await model.PcOptimization.ApplyAsync();
        if (native.Mutations != 0 || fallbackPrompts != beforePrompts)
            throw new InvalidOperationException("Cancelar el punto no debe ofrecer continuar ni aplicar ajustes.");
        point.Result = new(RestorePointState.Created, 101, DateTimeOffset.UtcNow);
        await model.PcOptimization.ApplyAsync();
        if (native.Mutations != 1 || fallbackPrompts != beforePrompts || !model.Recovery.PointStatus.Contains("creado y verificado") || !model.Recovery.UndoCommand.CanExecute(null) || model.PcOptimization.Options.Any(row => row.Selected))
            throw new InvalidOperationException("Un punto nuevo verificado debe preceder al lote y habilitar Deshacer con una selección ya resuelta.");
        checks.Add("Creación verificada permite aplicar una vez; el resultado deja la selección resuelta y Deshacer disponible");
        var window = new MainWindow(model) { Width = 1000, Height = 680, ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); window.UpdateLayout();
            var undoButton = Visuals<Button>(window).SingleOrDefault(button => button.IsVisible && button.Command == model.Recovery.UndoCommand);
            if (undoButton is null || !FitsWindow(window, undoButton))
                throw new InvalidOperationException("Deshacer último cambio debe permanecer visible en Resumen a tamaño mínimo.");
            foreach (var theme in new[] { "Claro", "Oscuro" })
            {
                ThemeManager.Apply(theme);
                await RenderFixtureWindowAsync(window, Path.Combine(directory, $"recuperacion-creado-{theme.ToLowerInvariant()}.png"));
                var expected = ((SolidColorBrush)Application.Current.Resources["Text"]).Color;
                var resultTexts = Visuals<TextBlock>((DependencyObject)window.FindName("PcOptimizationSection"))
                    .Where(text => text.IsVisible && (text.Text == model.Recovery.PointStatus || text.Text == model.PcOptimization.Status)).ToArray();
                if (resultTexts.Length != 2 || resultTexts.Any(text => text.Foreground is not SolidColorBrush brush || brush.Color != expected))
                    throw new InvalidOperationException("El resultado y el punto de restauración deben usar tinta del tema, también en oscuro.");
            }
            await model.Recovery.UndoAsync();
            if (native.Mutations != 1) throw new InvalidOperationException("Rechazar Deshacer no debe restaurar nada.");
            undo = true;
            await model.Recovery.UndoAsync();
            if (native.Mutations != 2 || model.Recovery.UndoCommand.CanExecute(null) || !model.Recovery.UndoStatus.Contains("restored"))
                throw new InvalidOperationException("Deshacer confirmado debe restaurar un cambio y deshabilitarse sin respaldo pendiente.");
            checks.Add("Deshacer contextual rechaza sin mutar y restaura solo el último respaldo después de confirmar");
            model.PcOptimization.Options.Single(row => row.Option.Id == "game-mode").Selected = true;
            point.Result = new(RestorePointState.Reused, 42, DateTimeOffset.UtcNow.AddHours(-1)); fallback = true;
            await model.PcOptimization.ApplyAsync();
            if (native.Mutations != 3 || !model.Recovery.PointStatus.Contains("Elegiste continuar"))
                throw new InvalidOperationException("Continuar sin punto nuevo requiere su propia confirmación expresa.");
            window.UpdateLayout();
            if (!Visuals<Button>(window).Any(button => button.IsVisible && button.Command == model.Recovery.OpenProtectionCommand))
                throw new InvalidOperationException("La herramienta Protección del sistema ofrecida tras reutilización debe estar disponible también sin un error nativo.");
            checks.Add("Solo una segunda confirmación explícita permite continuar con journal cuando Windows reutiliza el punto");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "recuperacion-reutilizado-consentido.png"));
            native.UndoConflict = true;
            await model.Recovery.UndoAsync();
            if (native.Mutations != 3 || !model.Recovery.HasUndoError || !model.Recovery.UndoStatus.Contains("cambió"))
                throw new InvalidOperationException("Un conflicto de Deshacer debe conservar el cambio externo y mostrar el error.");
            checks.Add("Conflicto de Deshacer conserva el estado externo y el respaldo, sin anunciar restauración");
            native.UndoConflict = false; native.UndoPartial = true;
            await model.Recovery.UndoAsync();
            if (!model.Recovery.HasUndoError || !model.Recovery.UndoStatus.StartsWith("Restauración parcial", StringComparison.Ordinal))
                throw new InvalidOperationException("Un deshacer parcial debe identificarse literalmente y conservar el respaldo.");
            checks.Add("Deshacer parcial se identifica literalmente y conserva acceso al respaldo pendiente");
            native.UndoPartial = false; invalidateUndo = true;
            var beforeUndoCalls = native.UndoCalls;
            await model.Recovery.UndoAsync();
            if (native.UndoCalls != beforeUndoCalls || model.Recovery.UndoCommand.CanExecute(null))
                throw new InvalidOperationException("Un respaldo desaparecido tras confirmar no debe redirigir Deshacer hacia otro ID.");
            checks.Add("El ID confirmado se vuelve a comprobar; un respaldo desaparecido no redirige ni ejecuta Deshacer");
            native.HideBackups = false; invalidateUndo = false;
            await model.Recovery.RefreshAsync(CancellationToken.None);
            model.PcOptimization.Options.Single(row => row.Option.Id == "xbox-captures").Selected = true;
            var mutations = native.Mutations;
            point.Block = true;
            var cancelled = model.PcOptimization.ApplyAsync(); await point.Started.Task;
            model.CancelCommand.Execute(null); await cancelled;
            if (native.Mutations != mutations || !model.Recovery.PointStatus.Contains("cancelada"))
                throw new InvalidOperationException("Cancelar mientras se verifica el punto no puede iniciar el lote.");
            checks.Add("Cancelación durante el preflight simulado termina sin iniciar ningún ajuste");
            point.Block = false; point.Result = new(RestorePointState.Disabled); fallback = false;
            await model.PcOptimization.ApplyAsync();
            foreach (var theme in new[] { "Claro", "Oscuro" })
            {
                ThemeManager.Apply(theme);
                await RenderFixtureWindowAsync(window, Path.Combine(directory, $"recuperacion-no-creado-{theme.ToLowerInvariant()}.png"));
            }
            window.Width = 1240; window.Height = 820;
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "recuperacion-no-creado-amplio.png"));
        }
        finally { window.Hide(); window.Close(); ThemeManager.Apply("Claro"); }
    }

    private sealed class FixtureRestorePointService : IRestorePointService
    {
        public RestorePointResult Result { get; set; } = new(RestorePointState.Created, 101, DateTimeOffset.UtcNow);
        public int Calls { get; private set; }
        public bool Block { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<RestorePointResult> PrepareAsync(CancellationToken cancellationToken = default)
        { Calls++; cancellationToken.ThrowIfCancellationRequested(); if (Block) { Started.TrySetResult(); await Task.Delay(Timeout.Infinite, cancellationToken); } return Result; }
    }

    private static async Task RunSummaryErrorChecksAsync(string directory, List<string> checks)
    {
        using var model = new MainViewModel(Path.Combine(directory, "summary-error"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            Collect = (_, _, _) => Task.FromException<SystemSnapshot>(new InvalidOperationException("No se pudo completar la lectura. Reintenta la detección."))
        });
        await model.InitializeAsync(autoScan: true);
        var window = new MainWindow(model) { Width = 1000, Height = 680, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); window.UpdateLayout();
            if (window.FindName("SummaryErrorPanel") is not SummaryErrorNotice { IsVisible: true } notice)
                throw new InvalidOperationException("Resumen debe mostrar su propio panel de error, aislado del aviso de otras vistas.");
            checks.Add("Resumen tiene un panel de error propio sin cambiar los avisos de otras vistas");
            foreach (var theme in new[] { "Claro", "Oscuro" })
            {
                ThemeManager.Apply(theme);
                if (!ReferenceEquals(notice.Resources["ErrorText"], Application.Current.Resources["Text"]) ||
                    !ReferenceEquals(notice.Resources["ErrorLine"], Application.Current.Resources["ControlLine"]) ||
                    !ReferenceEquals(notice.Resources["ErrorSurface"], Application.Current.Resources["Raised"]) ||
                    !ReferenceEquals(Application.Current.Resources["ErrorText"], Application.Current.Resources["Accent"]))
                    throw new InvalidOperationException("Los errores de Resumen deben ser neutros sin cambiar los de otras vistas, incluso al alternar tema.");
                if (((TextBlock)notice.FindName("ErrorHeading")).FontWeight != FontWeights.SemiBold)
                    throw new InvalidOperationException("El rótulo Error debe tener peso Semibold.");
                foreach (var mark in new[] { false, true })
                {
                    notice.ShowMark = mark;
                    await RenderFixtureWindowAsync(window, Path.Combine(directory, $"resumen-error-{theme.ToLowerInvariant()}-{(mark ? "con" : "sin")}-marca.png"));
                    var marker = (Border)notice.FindName("Mark");
                    if (marker.IsVisible != mark || marker.Width != 3)
                        throw new InvalidOperationException("La variante de marca debe ser opcional y medir tres unidades.");
                }
                checks.Add($"Error de Resumen en {theme}: tinta, borde y superficie neutrales; variantes con y sin marca de tres unidades");
            }
            notice.ShowMark = false;
            window.Width = 1240; window.Height = 820;
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "resumen-error-oscuro-amplio.png"));
            ThemeManager.ApplyContrastFixture();
            if (!ReferenceEquals(notice.Resources["ErrorText"], SystemColors.WindowTextBrush))
                throw new InvalidOperationException("El error local debe respetar alto contraste.");
            checks.Add("Error local conserva los colores del sistema en alto contraste");
            ThemeManager.Apply("Claro");
            window.Width = 1000; window.Height = 680;
            var originalMessage = notice.Message;
            var longMessage = string.Join(" ", Enumerable.Repeat("No se pudo completar la lectura de este dispositivo. Reintenta la detección; conserva el informe local si el problema continúa.", 20));
            notice.SetCurrentValue(SummaryErrorNotice.MessageProperty, longMessage);
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "resumen-error-texto-largo.png"));
            var dismiss = Visuals<Button>(notice).Single(button => button.IsVisible);
            var overflow = Visuals<ScrollViewer>(notice).Single();
            if (!FitsWindow(window, dismiss) || overflow.ScrollableHeight <= 0 || !dismiss.Focusable || !dismiss.IsTabStop ||
                !Visuals<TextBlock>(notice).Any(text => text.Text == longMessage))
                throw new InvalidOperationException("Un error extenso debe conservar el texto completo desplazable y el cierre accesible por teclado.");
            notice.SetCurrentValue(SummaryErrorNotice.MessageProperty, originalMessage);
            checks.Add("Error extenso conserva el texto completo con desplazamiento y Cerrar aviso accesible en la ventana mínima");
            model.SelectedPage = 1; window.UpdateLayout();
            if (notice.IsVisible || !model.HasError)
                throw new InvalidOperationException("Cambiar de vista debe ocultar únicamente el aviso local, sin borrar el error.");
            model.SelectedPage = 0; window.UpdateLayout();
            model.ClearErrorCommand.Execute(null); window.UpdateLayout();
            if (notice.IsVisible || model.HasError)
                throw new InvalidOperationException("Cerrar aviso debe eliminar y ocultar el error de Resumen.");
            checks.Add("Navegar conserva el error; Cerrar aviso lo elimina sin cambios del sistema");
        }
        finally { window.Hide(); window.Close(); ThemeManager.Apply("Claro"); }
    }

    private static async Task RunSafetySelectionChecksAsync(string directory, List<string> checks)
    {
        var native = new FixtureOptimizationService(); var approved = false; var restarts = 0;
        var preferences = new MemoryUserPreferencesStore();
        using var model = new MainViewModel(Path.Combine(directory, "manual-safety"), new ViewModelServices
        {
            CreateNativeTools = () => native, CreatePreferences = () => preferences,
            ConfirmAction = (_, _) => approved, RequestRestart = () => restarts++
        });
        model.ApplySnapshot(new SystemSnapshot(), isCurrent: true);
        await model.Tools.RefreshAsync();
        var row = model.Tools.GamingToggles.First(item => item.Setting.Id == "game-mode");
        model.Tools.EnableToggleCommand.Execute(row);
        if (!model.Tools.HasPendingManual || native.Mutations != 0) throw new InvalidOperationException("Añadir un ajuste manual debe preparar sin mutar.");
        checks.Add("El optimizador manual prepara su propia selección sin modificar Windows");
        await model.Tools.ApplyManualAsync();
        if (native.Mutations != 0 || !model.Tools.HasPendingManual) throw new InvalidOperationException("Rechazar la confirmación debe conservar la selección sin aplicarla.");
        approved = true; await model.Tools.ApplyManualAsync();
        if (native.Mutations != 1 || model.Tools.HasPendingManual || !model.Restart.HasChanges || restarts != 0) throw new InvalidOperationException("Aplicar manual debe ejecutar una vez y no reiniciar por su cuenta.");
        checks.Add("Aplicar selección manual confirma y registra solo los cambios ejecutados sin reinicio automático");
        model.Restart.RestartOnCloseCommand.Execute(null); model.Restart.CancelCommand.Execute(null);
        model.Restart.FinishClose(false);
        if (restarts != 0 || model.Restart.OnClose) throw new InvalidOperationException("Cancelar reinicio debe impedir cualquier solicitud al cerrar.");
        approved = false; model.Restart.RestartNowCommand.Execute(null);
        model.Restart.RestartOnCloseCommand.Execute(null); model.Restart.FinishClose(false);
        if (restarts != 0) throw new InvalidOperationException("Un reinicio rechazado no puede llegar a Windows.");
        checks.Add("Reiniciar ahora o al cerrar exige elección explícita y puede cancelarse sin solicitar reinicio real");
        await model.Settings.ToggleExpertAsync(); model.SelectedPage = 3;
        var window = new MainWindow(model) { Width = 1000, Height = 680, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); window.UpdateLayout();
            model.Settings.OpenManualCommand.Execute(null);
            await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            window.UpdateLayout();
            var manualSection = (FrameworkElement)window.FindName("ExpertManualOptions");
            var manualTop = manualSection.TransformToAncestor(window).Transform(new Point()).Y;
            if (manualTop < 0 || manualTop >= window.ActualHeight - 80) throw new InvalidOperationException("Abrir el manual debe llevar al usuario a su selección.");
            checks.Add("Abrir el optimizador manual desplaza la vista hasta sus controles");
            if (!Visuals<Button>(window).Any(button => button.Command == model.Tools.ApplyManualCommand && button.IsVisible)) throw new InvalidOperationException("Falta Aplicar en el manual experto.");
            checks.Add("El modo experto muestra su botón Aplicar selección manual separado del optimizador principal");
            ThemeManager.Apply("Oscuro"); window.UpdateLayout();
            var ink = ((SolidColorBrush)Application.Current.FindResource("Ink")).Color;
            var text = ((SolidColorBrush)Application.Current.FindResource("Text")).Color;
            var accent = ((SolidColorBrush)Application.Current.FindResource("Accent")).Color;
            var onAccent = ((SolidColorBrush)Application.Current.FindResource("White")).Color;
            static double Luminance(Color color)
            {
                static double Channel(byte value) { var c = value / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
                return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
            }
            static double Contrast(Color a, Color b) { var x = Luminance(a); var y = Luminance(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
            if (Contrast(ink, text) < 4.5 || Contrast(accent, onAccent) < 4.5) throw new InvalidOperationException("La paleta oscura no alcanza contraste de texto AA.");
            checks.Add("La paleta oscura conserva contraste AA para texto principal y botones primarios");
            var logoImage = (DrawingImage)Application.Current.FindResource("SilenLogo");
            if (logoImage.Drawing is not GeometryDrawing { Brush: SolidColorBrush logoBrush } || logoBrush.Color != text)
                throw new InvalidOperationException("El logo debe actualizar su tinta al cambiar de tema.");
            checks.Add("El logo conserva su geometría y cambia a tinta clara en modo oscuro");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "seguridad-manual-oscuro-minimo.png"));
            ThemeManager.ApplyContrastFixture(); window.UpdateLayout();
            if (((SolidColorBrush)Application.Current.FindResource("Text")).Color != SystemColors.WindowTextColor || ((SolidColorBrush)Application.Current.FindResource("Ink")).Color != SystemColors.WindowColor)
                throw new InvalidOperationException("El alto contraste debe usar los colores del sistema.");
            checks.Add("El alto contraste emplea colores del sistema sin modificar la configuración de Windows");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "seguridad-alto-contraste-minimo.png"));
            ThemeManager.Apply("Claro");
        }
        finally { ThemeManager.Apply("Claro"); window.Hide(); window.Close(); }
    }

    private static async Task RunRecoverySelectionChecksAsync(string directory, Dispatcher dispatcher, List<string> checks)
    {
        var native = new FixtureOptimizationService(); var approved = false; var prompts = new List<string>();
        native.RecoveryItems.Add(new("recovery-one", "fixture/one.tmp", 1024, DateTimeOffset.UtcNow));
        native.RecoveryItems.Add(new("recovery-two", "fixture/two.tmp", 2048, DateTimeOffset.UtcNow));
        using var model = new MainViewModel(Path.Combine(directory, "recovery-safety"), new ViewModelServices
        {
            CreateNativeTools = () => native,
            ConfirmAction = (heading, _) => { prompts.Add(heading); return approved; }
        });
        await model.Tools.RefreshAsync();
        var tools = model.Tools;
        if (tools.RecoveryFiles.Count != 2 || tools.RestoreRecoveryCommand.CanExecute(null) || tools.PurgeRecoveryCommand.CanExecute(null))
            throw new InvalidOperationException("Recuperación no puede preseleccionar archivos ni permitir un lote vacío.");
        checks.Add("Recuperación carga los archivos sin preseleccionar ni habilitar una acción vacía");
        async Task Run(ICommand command)
        {
            command.Execute(null);
            while (model.IsBusy) await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        }
        tools.RecoveryFiles[0].Selected = true;
        await Run(tools.PurgeRecoveryCommand);
        if (native.PurgedIds.Count != 0 || tools.RecoveryFiles.Count != 2) throw new InvalidOperationException("Cancelar la purga no debe eliminar nada.");
        checks.Add("Rechazar la confirmación de purga conserva todos los archivos recuperables");
        approved = true; await Run(tools.RestoreRecoveryCommand);
        if (!native.RestoredIds.SequenceEqual(["recovery-one"]) || tools.RecoveryFiles.Count != 1 || native.PurgedIds.Count != 0)
            throw new InvalidOperationException("Restaurar debe enviar solo los IDs elegidos sin purgar.");
        checks.Add("Restaurar envía únicamente los archivos elegidos y actualiza su lista");
        tools.RecoveryFiles[0].Selected = true; await Run(tools.PurgeRecoveryCommand);
        if (!native.PurgedIds.SequenceEqual(["recovery-two"]) || tools.HasRecoveryFiles || !prompts.SequenceEqual(["Eliminar definitivamente", "Restaurar temporales", "Eliminar definitivamente"]))
            throw new InvalidOperationException("La purga debe confirmar su propio lote y distinguirse de restaurar.");
        checks.Add("Eliminar definitivamente confirma su propio lote separado de restaurar");
    }

    private static async Task RunEarlyScanChecksAsync(string directory, Dispatcher dispatcher, List<string> checks)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProgress<ScanProgress>? current = null;
        var attempt = 0;
        var native = new FixtureOptimizationService();
        var fixture = new SystemSnapshot
        {
            Machine = new() { Manufacturer = "Equipo de prueba", Model = "Modelo", CpuName = "Procesador de prueba", GpuName = "Gráficos de prueba", TotalMemoryGb = 16 },
            Drives = [new() { Name = "C:", FreeGb = 50, TotalGb = 100 }],
            Samples = Enumerable.Range(0, 10).Select(index => new MetricSample { CpuPercent = 32, MemoryPercent = 61 }).ToList()
        };
        using var model = new MainViewModel(Path.Combine(directory, "early-scan"), new ViewModelServices
        {
            CreateNativeTools = () => native, ConfirmAction = (_, _) => true,
            Collect = async (_, token, detailed) =>
            {
                attempt++; current = detailed;
                detailed?.Report(new("Equipo", "Identidad leída. Midiendo el resto del equipo…", 1, 14, Machine: fixture.Machine));
                detailed?.Report(new("Unidades", "Capacidad y espacio libre leídos. Midiendo el resto del equipo…", 1, 14, Drives: fixture.Drives));
                started.TrySetResult(); await release.Task.WaitAsync(token);
                if (attempt == 3) throw new UnauthorizedAccessException("Fuente de diagnóstico no accesible.");
                return fixture with { Id = Guid.NewGuid() };
            },
            FindOem = (_, _, _) => Task.FromResult(new OemSupportResult(false, "Prueba", "", "", [], "Sin catálogo", false))
        });
        if (model.PcOptimization.PrepareCommand.CanExecute(null))
            throw new InvalidOperationException("Optimizar PC debe esperar al primer diagnóstico completo.");
        await model.Tools.RefreshAsync();
        model.Tools.EnableToggleCommand.Execute(model.Tools.GamingToggles.First(row => row.Setting.Id == "game-mode"));
        if (await model.PcOptimization.PrepareAsync() || await model.Tools.ApplyManualAsync() || native.Mutations != 0)
            throw new InvalidOperationException("Las llamadas directas tampoco pueden aplicar sin diagnóstico completo.");
        var scan = model.ScanAsync(); await started.Task;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (model.MachineTitle != "Equipo de prueba Modelo" || !model.MemoryDetail.Contains("16") || !model.DriveDetail.Contains("C:") || model.HasSnapshot || model.CpuValue != "Midiendo…" || model.MemoryTrace.Count != 0)
            throw new InvalidOperationException("La identidad, RAM y unidades deben aparecer antes de la primera muestra sin inventar lecturas.");
        checks.Add("Identidad, capacidad de RAM y unidades visibles antes del resultado completo; recursos aún Midiendo");
        var window = new MainWindow(model) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        window.Show();
        try
        {
            foreach (var theme in new[] { "Claro", "Oscuro" })
            foreach (var size in new[] { new Size(1240, 820), new Size(1000, 680) })
            {
                ThemeManager.Apply(theme); window.Width = size.Width; window.Height = size.Height;
                await RenderFixtureWindowAsync(window, Path.Combine(directory, $"carga-basicos-{theme}-{size.Width}x{size.Height}.png"));
                var prepare = Visuals<Button>(window).Single(button => button.IsVisible && button.Command == model.PcOptimization.PrepareCommand);
                if (prepare.IsEnabled) throw new InvalidOperationException("El botón visible de Optimizar PC debe estar desactivado durante la carga.");
            }
            for (var index = 0; index < 3; index++) current!.Report(new("Recursos", $"Muestra {index + 1} de 10 recibida.", index + 5, 14, fixture.Samples[index]));
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            if (!model.SampleDetail.Contains("Midiendo… 3/10") || !model.MetricSampleLabel.Contains("Midiendo") || model.HasCompletedResult || model.CpuTrace.Count != 3 || model.CpuValue != "32%")
                throw new InvalidOperationException("Una traza de tres muestras debe indicar Midiendo y análisis pendiente.");
            foreach (var theme in new[] { "Claro", "Oscuro" })
            {
                ThemeManager.Apply(theme);
                await RenderFixtureWindowAsync(window, Path.Combine(directory, $"carga-3-muestras-{theme}-1000x680.png"));
            }
        }
        finally { window.Hide(); ThemeManager.Apply("Claro"); }
        var old = current;
        model.CancelCommand.Execute(null); await scan;
        if (model.CpuTrace.Count != 0 || model.HasSnapshot || model.MachineTitle.Contains("Modelo") || model.DriveDetail.Contains("C:") || model.PcOptimization.PrepareCommand.CanExecute(null) || model.Tools.ApplyManualCommand.CanExecute(null) || !model.Status.Contains("cancelado"))
            throw new InvalidOperationException("Cancelar debe descartar las trazas parciales y conservar bloqueada la aplicación.");
        checks.Add("Diagnóstico parcial y cancelado no habilita Optimizar PC ni aplicar; muestras rotuladas Midiendo");
        started = new(TaskCreationOptions.RunContinuationsAsynchronously); release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        scan = model.ScanAsync(); await started.Task;
        old!.Report(new("Equipo antiguo", "Callback retrasado", 14, 14, fixture.Samples[0], Machine: new() { Manufacturer = "No debe verse" }));
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (model.MachineTitle.Contains("No debe verse") || model.CpuTrace.Count != 0 || model.ScanPercent == 100)
            throw new InvalidOperationException("El progreso de un análisis anterior no debe contaminar el siguiente.");
        release.TrySetResult(); if (!await scan || !model.CanApplyChanges || !model.HasCompletedResult || model.CpuTrace.Count != 10)
            throw new InvalidOperationException("Solo el diagnóstico terminado debe publicar promedios y habilitar las acciones.");
        await model.PcOptimization.PrepareAsync();
        if (!model.PcOptimization.ApplyCommand.CanExecute(null) || !model.Tools.ApplyManualCommand.CanExecute(null))
            throw new InvalidOperationException("Las selecciones preparadas deben habilitarse tras un diagnóstico completo.");
        checks.Add("Dos análisis seguidos aíslan generaciones y solo el resultado terminado habilita ambas selecciones");
        var saved = model.Snapshot;
        started = new(TaskCreationOptions.RunContinuationsAsynchronously); release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        scan = model.ScanAsync(); await started.Task;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (model.PcOptimization.ApplyCommand.CanExecute(null) || model.Tools.ApplyManualCommand.CanExecute(null) || model.CpuTrace.Count != 0)
            throw new InvalidOperationException("Un nuevo análisis bloquea incluso las selecciones ya preparadas y retira las trazas anteriores.");
        current!.Report(new("Recursos", "Muestra 1 de 10 recibida.", 5, 14, fixture.Samples[0]));
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        release.TrySetResult();
        if (await scan || model.CanApplyChanges || model.PcOptimization.ApplyCommand.CanExecute(null) || model.Tools.ApplyManualCommand.CanExecute(null) || !model.HasError || model.Snapshot != saved || !model.Status.Contains("último análisis guardado") || model.CpuTrace.Count != 10)
            throw new InvalidOperationException("Un fallo descarta parciales, identifica el análisis anterior y no reabre Aplicar.");
        checks.Add("Fallo tras datos parciales conserva solo el análisis anterior rotulado y bloquea las selecciones pendientes");
        window.Close();
    }

    private static async Task RunAutomaticStartupChecksAsync(string directory, List<string> checks)
    {
        var collected = 0;
        var native = new FixtureOptimizationService(); var preferences = new MemoryUserPreferencesStore();
        using var model = new MainViewModel(Path.Combine(directory, "automatic-startup"), new ViewModelServices
        {
            CreateNativeTools = () => native, CreatePreferences = () => preferences,
            Collect = (_, _, _) => { collected++; return Task.FromResult(new SystemSnapshot()); },
            FindOem = (_, _, _) => Task.FromResult(new OemSupportResult(false, "Prueba", "Prueba", "", [], "Sin catálogo de prueba", false))
        });
        await model.InitializeAsync(autoScan: true); await model.InitializeAsync(autoScan: true);
        if (collected != 1 || !model.HasSnapshot) throw new InvalidOperationException("El inicio debe detectar el equipo una vez sin pulsar Analizar.");
        checks.Add("El inicio detecta el equipo una única vez sin pedir un clic de análisis");
        var window = new MainWindow(model) { Width = 1000, Height = 680, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show();
            for (var page = 0; page < 6; page++)
            {
                model.SelectedPage = page; window.UpdateLayout();
                if (Visuals<Button>(window).Any(button => button.IsVisible && button.Command == model.ScanCommand)) throw new InvalidOperationException("Una vista sigue mostrando el botón de análisis repetido.");
            }
            checks.Add("Ninguna de las seis vistas repite el botón Analizar equipo");
            var logo = Visuals<Button>(window).Single(button => System.Windows.Automation.AutomationProperties.GetName(button) == "Abrir ajustes de Silen");
            logo.Command.Execute(logo.CommandParameter); window.UpdateLayout();
            if (model.SelectedPage != 3 || model.Settings.ExpertMode || Visuals<Button>(window).Any(button => button.IsVisible && button.Command == model.Settings.OpenManualCommand)) throw new InvalidOperationException("El logo debe abrir Ajustes en modo normal sin optimizador manual.");
            checks.Add("El wordmark abre Ajustes y el botón manual permanece oculto en modo normal");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "ajustes-silen-normal.png"));
            await model.Settings.ToggleExpertAsync(); model.Settings.OpenManualCommand.Execute(null); window.UpdateLayout();
            if (!model.Settings.ShowManual || window.FindName("ExpertManualOptions") is not FrameworkElement { IsVisible: true } || native.Mutations != 0 || !(await preferences.ReadAsync()).ExpertMode) throw new InvalidOperationException("El modo experto debe persistir y revelar el control manual sin aplicar ajustes.");
            checks.Add("El modo experto guarda la preferencia y revela controles manuales sin modificar Windows");
            ((FrameworkElement)window.FindName("ExpertManualOptions")).BringIntoView();
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "ajustes-silen-experto.png"));
            await model.Settings.ToggleExpertAsync(); window.UpdateLayout();
            if (model.Settings.ShowManual || window.FindName("UndoSection") is not FrameworkElement { IsVisible: true }) throw new InvalidOperationException("Salir del modo experto debe conservar la restauración accesible.");
            checks.Add("La restauración continúa disponible al desactivar el modo experto");
            model.SelectedPage = 0; window.UpdateLayout();
            if (window.FindName("EquipmentOverview") is not FrameworkElement equipment || window.FindName("PcOptimizationSection") is not FrameworkElement optimizer || equipment.TransformToAncestor(window).Transform(new Point()).Y >= optimizer.TransformToAncestor(window).Transform(new Point()).Y) throw new InvalidOperationException("El equipo debe verse antes del optimizador al abrir.");
            checks.Add("Resumen presenta el equipo detectado antes de la selección de optimización");
            var summaryScroll = (ScrollViewer)window.FindName("SummaryScroll");
            var prepare = Visuals<Button>(window).Single(button => button.IsVisible && button.Command == model.PcOptimization.PrepareCommand);
            var actionBounds = prepare.TransformToAncestor(summaryScroll).TransformBounds(new Rect(prepare.RenderSize));
            if (actionBounds.Top < 0 || actionBounds.Bottom > summaryScroll.ViewportHeight)
                throw new InvalidOperationException("Optimizar PC debe verse completo sin desplazamiento al abrir Resumen en el tamaño mínimo.");
            checks.Add("Optimizar PC queda completo en el primer viewport de Resumen a 1000×680");
        }
        finally { window.Hide(); window.Close(); }
        var failRead = true; var attempts = 0;
        using var failed = new MainViewModel(Path.Combine(directory, "automatic-startup-failure"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            Collect = (_, _, _) => { attempts++; return failRead ? Task.FromException<SystemSnapshot>(new InvalidOperationException("Fallo de lectura controlado")) : Task.FromResult(new SystemSnapshot()); },
            FindOem = (_, _, _) => Task.FromResult(new OemSupportResult(false, "Prueba", "Prueba", "", [], "Sin catálogo", false))
        });
        await failed.InitializeAsync(autoScan: true);
        if (!failed.ShowRetryDetection || !failed.HasError || !failed.Error.Contains("Fallo de lectura controlado")) throw new InvalidOperationException("Un fallo del diagnóstico inicial debe conservar su error y permitir reintento.");
        failRead = false; failed.RetryDetectionCommand.Execute(null); while (failed.IsBusy) await Task.Delay(10);
        if (!failed.HasSnapshot || failed.ShowRetryDetection || attempts != 2) throw new InvalidOperationException("El reintento debe recuperar el diagnóstico inicial sin repetirlo automáticamente.");
        checks.Add("El diagnóstico inicial conserva errores y permite recuperarlos con un reintento explícito");
    }

    private static async Task RunPcOptimizationChecksAsync(string directory, List<string> checks)
    {
        void CheckPc(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Fallo de interacción del PC: " + name);
            checks.Add(name);
        }
        var native = new FixtureOptimizationService(includeChrome: true);
        var game = new FixtureFortniteProfileService(directory);
        var approved = false;
        var prompts = 0;
        var elevationRequests = 0;
        var cancelElevation = true;
        IReadOnlyList<PcOptimizationOption> confirmed = [];
        using var model = new MainViewModel(Path.Combine(directory, "pc-history"), new ViewModelServices
        {
            CreateNativeTools = () => native, CreateFortniteProfile = () => game,
            CreatePcOptimization = tools => new PcOptimizationService(tools, () => false),
            ConfirmPcOptimization = selection => { prompts++; confirmed = selection; return approved; },
            CreateRestorePoint = () => new FixtureRestorePointService(),
            OpenElevated = () => { elevationRequests++; if (cancelElevation) throw new System.ComponentModel.Win32Exception(1223); },
            ResolvePresentMon = () => Environment.ProcessPath!, IsElevated = () => false,
            Open = (_, _) => throw new InvalidOperationException("La optimización del PC no debe abrir herramientas externas.")
        });
        var pc = model.PcOptimization;
        CheckPc(!pc.PrepareCommand.CanExecute(null) && !pc.ApplyCommand.CanExecute(null), "Optimizar PC espera al diagnóstico completo; aplicar requiere además preparar una selección");
        model.ApplySnapshot(new SystemSnapshot(), isCurrent: true);
        var window = new MainWindow(model) { Width = 1000, Height = 680, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show();
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "pc-inicio-minimo.png"));
            CheckPc(Visuals<Button>(window).Any(button => button.IsVisible && button.Command == pc.PrepareCommand), "Resumen expone el botón conectado al flujo real del PC");
            await pc.PrepareAsync();
            CheckPc(pc.HasPreview && native.Mutations == 0 && game.ApplyCalls == 0 && prompts == 0, "Preparar la optimización no modifica Windows ni aplica el perfil de un juego");
            CheckPc(pc.Options.Single(row => row.Option.Id == "game-mode").Selected && !pc.Options.Single(row => row.Option.Id == "xbox-captures").Selected, "La selección recomendada conserva capturas y cambios opcionales sin elegir");
            CheckPc(!pc.Options.Single(row => row.Option.Id == "chrome-background").Selected &&
                !pc.Options.Single(row => row.Option.Id.StartsWith("startup:")).Selected,
                "Chrome y cada entrada de inicio requieren selección manual en el optimizador principal");
            var unavailable = pc.Options.Single(row => row.Option.Id == "power-mode");
            unavailable.Selected = true;
            CheckPc(!unavailable.Selected, "No se puede seleccionar una opción de energía no disponible");
            pc.ClearSelectionCommand.Execute(null);
            CheckPc(pc.SelectionSummary.StartsWith("0 seleccionados", StringComparison.Ordinal), "El contador usa plural con cero seleccionados");
            var grammarChoices = pc.Options.Where(row => row.Option.Available).Take(3).ToArray();
            grammarChoices[0].Selected = true;
            CheckPc(pc.SelectionSummary.StartsWith("1 seleccionado ·", StringComparison.Ordinal), "El contador usa singular con un seleccionado");
            foreach (var choice in grammarChoices) choice.Selected = true;
            CheckPc(pc.SelectionSummary.StartsWith("3 seleccionados ·", StringComparison.Ordinal), "El contador usa plural con tres seleccionados");
            pc.ClearSelectionCommand.Execute(null);
            CheckPc(!pc.ApplyCommand.CanExecute(null), "La selección vacía desactiva Aplicar selección");
            pc.Options.Single(row => row.Option.Id == "xbox-captures").Selected = true;
            await pc.PrepareAsync();
            CheckPc(pc.Options.Single(row => row.Option.Id == "xbox-captures").Selected && !pc.Options.Single(row => row.Option.Id == "game-mode").Selected, "Actualizar disponibilidad conserva la selección del usuario");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "pc-seleccion-minimo.png"));
            var applyButton = Visuals<Button>(window).Single(button => button.IsVisible && button.Command == pc.ApplyCommand);
            var applyPosition = applyButton.TransformToAncestor(window).Transform(new Point());
            CheckPc(applyPosition.Y >= 0 && applyPosition.Y + applyButton.ActualHeight < window.ActualHeight - 100, "Aplicar selección permanece accesible en el tamaño mínimo de ventana");
            var summaryScroll = (ScrollViewer)window.FindName("SummaryScroll");
            summaryScroll.ScrollToEnd(); window.UpdateLayout();
            if (!FitsWindow(window, applyButton) || applyButton.TransformToAncestor(window).Transform(new Point()).Y != applyPosition.Y)
                throw new InvalidOperationException("Aplicar selección debe permanecer accesible al recorrer todos los ajustes.");
            CheckPc(!Visuals<ScrollViewer>(summaryScroll).Any(view => view != summaryScroll && view.IsVisible && view.ScrollableHeight > 0), "Los ajustes de Resumen usan un solo desplazamiento y Aplicar permanece visible al recorrerlos");
            summaryScroll.ScrollToTop(); window.UpdateLayout();
            await pc.ApplyAsync();
            CheckPc(prompts == 1 && native.Mutations == 0 && confirmed.Select(option => option.Id).SequenceEqual(["xbox-captures"]), "La confirmación enumera solo los cambios elegidos y rechazarla no aplica nada");
            approved = true;
            await pc.ApplyAsync();
            CheckPc(native.Mutations == 1 && game.ApplyCalls == 0 && pc.Steps.Count == 1 && pc.Steps.Single().Changed && !pc.HasActionError, "Aplicar PC ejecuta únicamente la selección y expone el resultado confirmado");
            CheckPc(model.Tools.HasChanges && model.Tools.Changes.Single().CanUndo, "El resultado actualiza los respaldos disponibles para restaurar");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "pc-resultado-minimo.png"));
            var resultText = Visuals<TextBlock>(window).First(text => text.IsVisible && text.Text == pc.Status);
            resultText.BringIntoView(); window.UpdateLayout();
            var resultPosition = resultText.TransformToAncestor(window).Transform(new Point());
            CheckPc(resultPosition.Y >= 0 && resultPosition.Y + resultText.ActualHeight < window.ActualHeight - 100 && FitsWindow(window, applyButton), "El resultado es accesible por desplazamiento con el diagnóstico completo y Aplicar permanece fijo");
            pc.ClearSelectionCommand.Execute(null);
            pc.Options.Single(row => row.Option.Id == "game-mode").Selected = true;
            native.FailNext = true;
            await pc.ApplyAsync();
            CheckPc(pc.HasActionError && pc.Steps.Single().Success == false && pc.Steps.Single().Changed == false, "Un permiso denegado queda como error por paso sin afirmar que se aplicó");
            window.UpdateLayout();
            var actionError = Visuals<SummaryErrorNotice>(window).FirstOrDefault(panel => panel.IsVisible && panel.Message == pc.Status);
            CheckPc(actionError is not null && ReferenceEquals(actionError.Resources["ErrorText"], Application.Current.Resources["Text"]), "Un fallo al aplicar usa el mismo aviso neutral de Resumen, con rótulo Error");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "resumen-error-aplicacion.png"));
            model.SelectedPage = 3;
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "pc-ajustes-minimo.png"));
            CheckPc(!Visuals<Button>(window).Any(button => button.IsVisible && (button.Command == pc.ApplyCommand || button.Command == pc.PrepareCommand)), "Ajustes conserva controles individuales sin duplicar el flujo principal de Resumen");
            CheckPc(model.OpenOptimizationElevatedCommand.CanExecute(null) && !model.CanRequestCaptureElevation && elevationRequests == 0, "Los permisos para optimización se pueden solicitar sin provocar un error de captura");
            model.OpenOptimizationElevatedCommand.Execute(null);
            while (model.IsBusy) await Task.Delay(10);
            CheckPc(elevationRequests == 1 && model.Status.Contains("cancelada") && !model.HasCaptureError && native.Mutations == 1, "Cancelar el permiso de optimización no modifica ajustes ni el estado de captura");
            cancelElevation = false;
            // IsBusy termina dentro del delegado antes de que AsyncCommand libere su propio bloqueo.
            while (!model.OpenOptimizationElevatedCommand.CanExecute(null)) await Task.Delay(10);
            model.OpenOptimizationElevatedCommand.Execute(null);
            while (model.IsBusy) await Task.Delay(10);
            CheckPc(elevationRequests == 2 && model.Status.Contains("esta sesión sigue disponible") && !model.HasCaptureError && native.Mutations == 1, "La elevación explícita abre otra sesión sin aplicar la selección automáticamente");
            model.SelectedPage = 0;
            await pc.PrepareAsync(); pc.ClearSelectionCommand.Execute(null);
            pc.Options.Single(row => row.Option.Id == "chrome-background").Selected = true;
            pc.Options.Single(row => row.Option.Id.StartsWith("startup:")).Selected = true;
            await pc.ApplyAsync();
            CheckPc(confirmed.Count == 2 && pc.Steps.Count == 2 && pc.Steps.All(step => step.Success && step.Changed) && native.Mutations == 3,
                "La confirmación del botón principal aplica solo Chrome y la entrada de inicio elegida");
            CheckPc(!pc.Options.Any(row => row.Option.Id.StartsWith("startup:")) && pc.Options.Single(row => row.Option.Id == "chrome-background").Option.AlreadyApplied,
                "El resultado retira la entrada desactivada y detecta la política Chrome ya aplicada");
            CheckPc(model.Tools.Changes.Count == 3 && model.Tools.StartupEntries.Single().Entry.Enabled == false,
                "Los respaldos y el estado de inicio se actualizan después de la selección principal");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "pc-inicio-chrome-resultado.png"));
            native.BlockInspection = true;
            var preparing = pc.PrepareAsync();
            await native.InspectionStarted.Task;
            ((FrameworkElement)window.FindName("PcOptimizationSection")).BringIntoView(); window.UpdateLayout();
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "pc-preparando-minimo.png"));
            var progressBar = Visuals<System.Windows.Controls.ProgressBar>(window).First(bar => bar.IsVisible && System.Windows.Automation.AutomationProperties.GetName(bar) == "Progreso de optimización del PC");
            var progressPosition = progressBar.TransformToAncestor(window).Transform(new Point());
            CheckPc(model.IsBusy && !pc.ApplyCommand.CanExecute(null) && progressPosition.Y + progressBar.ActualHeight < window.ActualHeight - 100, "Preparar muestra progreso visible y bloquea aplicar mientras se consultan los ajustes");
            model.CancelCommand.Execute(null);
            await native.InspectionCancelled.Task;
            native.ReleaseInspection.TrySetResult();
            await preparing;
            CheckPc(!pc.HasPreview && !pc.ApplyCommand.CanExecute(null) && !pc.HasResults, "Cancelar la preparación descarta selecciones y resultados obsoletos sin aplicar cambios");
        }
        finally { window.Hide(); window.Close(); }
    }

    private static async Task RunUpdateGamingCleanupChecksAsync(string directory, List<string> checks)
    {
        void CheckPackage(bool condition, string text)
        {
            if (!condition) throw new InvalidOperationException("Fallo del paquete funcional: " + text);
            checks.Add(text);
        }
        var native = new FixtureOptimizationService
        {
            UpdateHealth = new(true, true, DateTimeOffset.UtcNow.AddDays(2), false, false, false, "Windows Update está pausado hasta la fecha indicada. El servicio está deshabilitado.")
        };
        var profile = new FixtureFortniteProfileService(directory) { MissingInstallation = true };
        using var model = new MainViewModel(Path.Combine(directory, "package-history"), new ViewModelServices
        {
            CreateNativeTools = () => native, CreateFortniteProfile = () => profile,
            ConfirmOptimization = () => true, ConfirmAction = (_, _) => true, ChooseGameExecutable = () => Environment.ProcessPath!,
            IsElevated = () => true, ResolvePresentMon = () => Environment.ProcessPath!,
            OpenElevatedForUpdates = () => throw new InvalidOperationException("No se debe solicitar UAC en la sesión elevada simulada."),
            Open = (_, _) => throw new InvalidOperationException("Estas acciones deben permanecer dentro de Silen.")
        });
        await model.InitializeAsync();
        CheckPackage(model.WindowsUpdate.Health.Paused && model.WindowsUpdate.ResumeCommand.CanExecute(null) && native.UpdateResumeCalls == 0, "El estado pausado/deshabilitado se muestra sin repararlo automáticamente");
        model.ApplySnapshot(new SystemSnapshot(), isCurrent: true);
        var window = new MainWindow(model) { Width = 1000, Height = 680, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        async Task ExecutePackage(ICommand command)
        {
            CheckPackage(command.CanExecute(null), "La acción funcional está disponible según el estado actual");
            command.Execute(null); while (model.IsBusy) await Task.Delay(10);
        }
        try
        {
            window.Show(); model.SelectedPage = 2;
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "update-pausado-minimo.png"));
            CheckPackage(Visuals<TextBlock>(window).Any(text => text.IsVisible && text.Text.Contains("está pausado")), "Drivers presenta el estado real de pausa antes de buscar ofertas");
            await model.WindowsUpdate.ResumeAsync();
            CheckPackage(native.UpdateResumeCalls == 1 && model.WindowsUpdate.Health.CanSearch && model.Tools.HasChanges, "Reanudar se solicita explícitamente y actualiza el estado comprobado y el respaldo");
            model.Tools.SelectedChange = model.Tools.Changes.Single();
            await ExecutePackage(model.Tools.UndoCommand);
            CheckPackage(model.WindowsUpdate.Health.Paused && model.WindowsUpdate.NeedsRecovery, "Restaurar el cambio de Windows Update actualiza inmediatamente su estado en Drivers");
            model.SelectedPage = 3;
            CheckPackage(model.Tools.DirectTempLabel.Contains("KiB") && native.CleanedIds.Count == 0, "La limpieza directa calcula el tamaño sin eliminar archivos");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "temporales-directos-minimo.png"));
            CheckPackage(Visuals<Button>(window).Count(button => button.IsVisible && button.Command == model.Tools.CleanAllTempCommand) == 1 &&
                !Visuals<Button>(window).Any(button => button.IsVisible && (button.Command == model.Tools.PreviewTempCommand || button.Command == model.Tools.CleanTempCommand)), "Temporales ofrece un único botón directo sin el antiguo selector técnico");
            await ExecutePackage(model.Tools.CleanAllTempCommand);
            CheckPackage(native.CleanedIds.ToHashSet().SetEquals(["first", "second"]) && model.Tools.CleanupStatus.Contains("En recuperación:") && model.Tools.CleanupStatus.Contains("Espacio liberado:"), "La limpieza directa usa la vista previa fresca y distingue espacio en recuperación de espacio liberado");
            native.CancelPreviewNext = true; await model.Tools.RefreshAsync();
            CheckPackage(!model.Tools.CleanAllTempCommand.CanExecute(null), "Un cálculo cancelado no convierte el tamaño desconocido en una limpieza disponible");
            await model.Tools.RefreshAsync();
            CheckPackage(model.Tools.CleanAllTempCommand.CanExecute(null), "Actualizar los ajustes permite recuperar el cálculo de temporales después de cancelarlo");
            CheckPackage(!window.Pages.Items.OfType<TabItem>().Any(item => item.Header?.ToString() == "Gaming"), "Gaming se retira y el optimizador principal concentra los ajustes");
            model.SelectedPage = 0;
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "resumen-unico-minimo.png"));
            CheckPackage(!Visuals<Button>(window).Any(button => button.IsVisible && button.Command == model.ExportCommand), "Resumen no expone exportaciones técnicas JSON o Markdown");
        }
        finally { window.Hide(); window.Close(); }
    }

    private static async Task RunBloomChecksAsync(string directory, List<string> checks)
    {
        void CheckBloom(bool condition, string name) { if (!condition) throw new InvalidOperationException("Bloom: " + name); checks.Add(name); }
        var keys = new FixtureBloomKeys(); var client = new FixtureBloomClient(); var tools = new FixtureBloomTools();
        var model = new MainViewModel(Path.Combine(directory, "bloom-history"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), CreateBloomKeys = () => keys,
            CreateBloomClient = () => client, CreateBloomTools = (_, _) => tools,
            OpenBloomProviderPage = _ => { keys.LinkOpened = true; }
        });
        var window = new MainWindow(model) { Width = 1000, Height = 680, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); model.SelectedPage = 5;
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "bloom-bienvenida-minimo.png"));
            CheckBloom(!model.Bloom.HasKey && Visuals<PasswordBox>(window).Any(box => box.IsVisible), "Sin clave, Bloom presenta bienvenida y un campo de contraseña");
            model.Bloom.OpenKeyPageCommand.Execute(null); CheckBloom(keys.LinkOpened, "El enlace de obtención de clave usa la acción oficial configurada");
            await model.Bloom.SaveKeyAsync("fixture-bloom-secret");
            CheckBloom(model.Bloom.HasKey && keys.Read() == "fixture-bloom-secret" && client.Requests == 0, "Guardar la clave no envía un mensaje ni ejecuta herramientas");
            model.Bloom.Input = "Revisa gaming"; await model.Bloom.SendAsync();
            CheckBloom(model.Bloom.HasPending && tools.Executions == 0 && model.Bloom.Messages.Any(message => message.Text.Contains("¿Deseas que aplique estos ajustes?")), "Una propuesta de Gemini no ejecuta cambios antes de confirmación local");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "bloom-propuesta-minimo.png"));
            CheckBloom(Visuals<TextBlock>(window).Any(text => text.IsVisible && text.Text == client.ModelLabel), "Bloom muestra el modelo del cliente y no una versión fijada en XAML");
            await model.Bloom.ConfirmAsync();
            CheckBloom(tools.Executions == 1 && !model.Bloom.HasPending, "Confirmar consume una sola propuesta y devuelve el resultado al chat");
            CheckBloom(!model.Bloom.Messages.Any(message => message.Text.Contains("fixture-bloom-secret")), "La clave no aparece en las burbujas del chat");
            await model.Bloom.NewConversationAsync(); model.Bloom.Input = "Otra propuesta"; client.Requests = 0; await model.Bloom.SendAsync(); await model.Bloom.CancelProposalAsync();
            CheckBloom(tools.Executions == 1 && !model.Bloom.HasPending, "Cancelar la propuesta no ejecuta su herramienta");
            await model.Bloom.DeleteKeyAsync();
            CheckBloom(!model.Bloom.HasKey && keys.Read() is null, "Eliminar la configuración vuelve a la bienvenida sin clave");
        }
        finally { window.Hide(); await model.PrepareToCloseAsync(); model.Dispose(); window.Close(); }
    }
    private static async Task RunRemainingViewChecksAsync(string directory, List<string> checks)
    {
        void CheckView(bool condition, string name) { if (!condition) throw new InvalidOperationException("Vistas restantes: " + name); checks.Add(name); }
        var native = new FixtureOptimizationService(); var keys = new FixtureBloomKeys(); var client = new FixtureBloomClient(); var tools = new FixtureBloomTools();
        using var model = new MainViewModel(Path.Combine(directory, "remaining-history"), new ViewModelServices
        {
            CreateNativeTools = () => native, CreatePreferences = () => new MemoryUserPreferencesStore(),
            CreateBloomKeys = () => keys, CreateBloomClient = () => client, CreateBloomTools = (_, _) => tools,
            FindUpdates = _ => Task.FromResult(new DriverUpdateResult(true, [], "Sin ofertas de prueba")), ConfirmAction = (_, _) => false
        });
        var window = new MainWindow(model) { Width = 1000, Height = 680, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); model.SelectedPage = 5; await model.Bloom.SaveKeyAsync("fixture-only-no-provider-call");
            window.UpdateLayout();
            CheckView(((ScrollViewer)window.FindName("BloomChatScroll")).ActualHeight >= 120,
                "Bloom conserva al menos 120 unidades para leer el chat vacío a 1000×680, con el compositor visible");
            var send = Visuals<Button>(window).Single(button => button.Command == model.Bloom.SendCommand);
            var sendBounds = send.TransformToAncestor(window).TransformBounds(new Rect(send.RenderSize));
            CheckView(sendBounds.Top > 0 && sendBounds.Bottom < window.ActualHeight - 70 && !send.IsEnabled,
                "Enviar queda completo en el área de conversación y deshabilitado sin mensaje");
            model.Reports.Add(new(Path.Combine(directory, "observacion-simulada.json"), DateTimeOffset.Now, "Equipo de prueba con un nombre de procesador largo para comprobar la lectura completa", 1));
            model.Actions.Add(new(DateTimeOffset.Now, "fixture", "Fuente oficial de prueba", "Guía abierta; no se comprobó ni aplicó un cambio."));
            model.ApplySnapshot(new SystemSnapshot { Machine = new() { Manufacturer = "Equipo de prueba", Model = "Modelo simulado" }, Drivers = [new() { DeviceName = "Adaptador de prueba", Provider = "Proveedor de prueba", Version = "1.0", Signed = true }] });
            model.SelectedPage = 4; window.UpdateLayout();
            var savedHeading = Visuals<TextBlock>(window).Single(text => text.IsVisible && text.Text == "Análisis guardados");
            var export = Visuals<Expander>(window).Single(item => item.Header?.ToString() == "Exportar el diagnóstico actual");
            CheckView(savedHeading.TransformToAncestor(window).Transform(new Point()).Y < export.TransformToAncestor(window).Transform(new Point()).Y,
                "Historial comienza por los análisis; exportar permanece como detalle secundario");
            model.SelectedReport = model.Reports[0];
            CheckView(model.LoadReportCommand.CanExecute(null), "Seleccionar un análisis habilita Abrir sin ejecutar una carga ni una acción Windows");
            model.SelectedPage = 2; window.UpdateLayout();
            var search = Visuals<Button>(window).Single(button => button.Command == model.SearchUpdatesCommand);
            var searchBounds = search.TransformToAncestor(window).TransformBounds(new Rect(search.RenderSize));
            CheckView(searchBounds.Top > 0 && searchBounds.Bottom < window.ActualHeight - 100 && search.Background == Application.Current.FindResource("Accent"),
                "Drivers muestra Buscar actualizaciones completo y como acción principal antes de tener ofertas");
            await model.SearchUpdatesAsync();
            CheckView(model.UpdateStatus.Contains("No hay actualizaciones") && !model.DownloadUpdatesCommand.CanExecute(null), "Drivers conserva el resultado vacío real y no ofrece descargarlo");
            model.WindowsUpdate.SetHealth(new(true, false, null, true, true, false, "Windows Update está disponible. El servicio de actualización está en ejecución."));
            await model.Tools.RefreshAsync(); await model.Settings.ToggleExpertAsync(); model.Settings.OpenManualCommand.Execute(null); model.SelectedPage = 3;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
            var settingsScroll = Visuals<ScrollViewer>((DependencyObject)((TabItem)window.Pages.Items[3]).Content).First();
            settingsScroll.ScrollToEnd(); window.UpdateLayout();
            var apply = Visuals<Button>(window).Single(button => button.Command == model.Tools.ApplyManualCommand);
            var applyBounds = apply.TransformToAncestor(window).TransformBounds(new Rect(apply.RenderSize));
            CheckView(applyBounds.Top > 0 && applyBounds.Bottom < window.ActualHeight - 70,
                "Aplicar selección manual permanece completo al desplazarse al final de Ajustes");
            model.Tools.EnableToggleCommand.Execute(model.Tools.GamingToggles.First(item => item.Setting.Id == "game-mode"));
            foreach (var theme in new[] { "Claro", "Oscuro" })
            {
                model.Settings.SelectedTheme = theme; ThemeManager.Apply(theme);
                foreach (var size in new[] { (Width: 1000, Height: 680, Name: "minimo"), (Width: 1240, Height: 820, Name: "amplio") })
                {
                    window.Width = size.Width; window.Height = size.Height;
                    foreach (var page in new[] { (Index: 2, Name: "drivers"), (Index: 3, Name: "ajustes"), (Index: 4, Name: "historial"), (Index: 5, Name: "bloom") })
                    {
                        model.SelectedPage = page.Index; window.UpdateLayout();
                        if (page.Index == 3) settingsScroll.ScrollToTop();
                        await RenderFixtureWindowAsync(window, Path.Combine(directory, $"remaining-{page.Name}-{theme.ToLowerInvariant()}-{size.Name}.png"));
                    }
                }
            }
            model.Bloom.Input = "Propón un ajuste"; await model.Bloom.SendAsync(); window.Width = 1000; window.Height = 680;
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "remaining-bloom-propuesta-minimo.png"));
            var confirm = Visuals<Button>(window).Single(button => button.Command == model.Bloom.ConfirmCommand);
            CheckView(model.Bloom.HasPending && confirm.IsVisible && tools.Executions == 0 && confirm.TransformToAncestor(window).TransformBounds(new Rect(confirm.RenderSize)).Bottom < window.ActualHeight - 70,
                "La propuesta de Bloom conserva su confirmación visible sin ejecutar herramientas");
            await model.Bloom.CancelProposalAsync();
            CheckView(!model.Bloom.HasPending && native.Mutations == 0 && tools.Executions == 0, "Refinar las vistas y cancelar la propuesta no aplica cambios del sistema");
            client.FailNext = true; model.Bloom.Input = "Respuesta con error"; await model.Bloom.SendAsync(); window.UpdateLayout();
            CheckView(model.Bloom.HasError && Visuals<SummaryErrorNotice>(window).Any(notice => notice.IsVisible && notice.Message == model.Bloom.Status), "Un error de Bloom usa el rótulo Error y el aviso neutral sin perder su explicación");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "remaining-bloom-error-minimo.png"));
        }
        finally { window.Hide(); await model.PrepareToCloseAsync(); window.Close(); ThemeManager.Apply("Claro"); }
    }

    private sealed class FixtureBloomKeys : IBloomKeyStore
    {
        private string? key; public bool LinkOpened;
        public string? Read() => key; public void Save(string value) => key = value; public void Delete() => key = null;
    }
    private sealed class FixtureBloomClient : IBloomClient
    {
        public int Requests;
        public bool FailNext;
        public string ModelLabel => "Modelo de prueba (sin red)";
        public Task<BloomReply> SendAsync(string key, IReadOnlyList<System.Text.Json.JsonElement> contents, CancellationToken token = default)
        {
            if (FailNext) { FailNext = false; throw new BloomException("No se pudo contactar con el proveedor de prueba. Revisa la conexión y vuelve a enviar el mensaje. No se ejecutó ninguna herramienta."); }
            return Task.FromResult(new BloomReply(System.Text.Json.JsonDocument.Parse(Requests++ == 0
            ? """{"role":"model","parts":[{"functionCall":{"name":"apply_optimizations","args":{"categories":["gaming"],"preserve_audio":true}}}]}"""
            : """{"role":"model","parts":[{"text":"El resultado confirma el cambio. Puedes restaurarlo desde Ajustes."}]}""").RootElement.Clone()));
        }
    }
    private sealed class FixtureBloomTools : IBloomTools
    {
        public int Executions;
        public Task<BloomAction> PrepareAsync(BloomCall call, CancellationToken token) => Task.FromResult(new BloomAction("Activar Modo Juego de Windows. Se guardará el estado anterior. No cambia el audio.", _ =>
        { Executions++; return Task.FromResult(System.Text.Json.JsonSerializer.SerializeToElement(new { success = true, message = "Modo Juego aplicado y respaldado (prueba controlada)." })); }));
    }

    private static async Task RunRamNetworkProductChecksAsync(string directory, List<string> checks)
    {
        void CheckUniversal(bool condition, string name) { if (!condition) throw new InvalidOperationException("RAM/red: " + name); checks.Add(name); }
        var native = new FixtureOptimizationService();
        var memoryReadFails = false;
        var detailsFail = false; var resolverFails = false; var blockDns = false;
        var dnsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new MainViewModel(Path.Combine(directory, "ram-network-history"), new ViewModelServices
        {
            CreateNativeTools = () => native, CreateInstalledProducts = () => new FixtureProductCatalog(),
            ReadMemory = automatic => memoryReadFails ? null : new(8L << 30, 300L << 20, 15L << 30, 16L << 30, automatic),
            ReadAutomaticPagefile = _ => Task.FromResult<bool?>(true),
            ReadMemoryProcesses = _ => detailsFail ? Task.FromException<IReadOnlyList<Silen.Core.ProcessInfo>>(new InvalidOperationException("No se pudieron consultar los procesos de prueba.")) : Task.FromResult<IReadOnlyList<Silen.Core.ProcessInfo>>([new("Juego de prueba", 101, 2048)]),
            GetDnsResolvers = () => resolverFails ? throw new InvalidOperationException("No se pudieron consultar los servidores de prueba.") : [new("DNS de prueba", "1.1.1.1")],
            MeasureDns = async (_, progress, token) =>
            {
                if (blockDns) { progress?.Report(new("DNS de prueba", "1.1.1.1", 1, 1, null, null, DateTimeOffset.Now)); dnsStarted.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
                var row = new DnsBenchmarkRow("DNS de prueba", "1.1.1.1", 3, 0, 12, 14, DateTimeOffset.Now); progress?.Report(row); return [row];
            }
        });
        var window = new MainWindow(model) { Width = 1000, Height = 680, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); model.SelectedPage = 1;
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "ram-red-vacio-minimo.png"));
            CheckUniversal(!model.RamNetwork.HasDnsRows && VisibleText(window, true).Any(text => text.Contains("Sin mediciones todavía")), "DNS vacío explica cómo iniciar sin presentar tiempos ficticios ni una tabla hueca");
            await model.RamNetwork.RefreshAsync(); for (var index = 0; index < 5; index++) model.RamNetwork.UpdateLive();
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "ram-red-minimo.png"));
            var ramView = Visuals<RamNetworkView>(window).Single();
            var ramViewport = Visuals<ScrollViewer>(ramView).First();
            var measureAction = Visuals<Button>(ramView).Single(button => button.Command == model.RamNetwork.MeasureCommand);
            var measureBounds = measureAction.TransformToAncestor(ramViewport).TransformBounds(new Rect(measureAction.RenderSize));
            CheckUniversal(measureBounds.Top >= 0 && measureBounds.Bottom <= ramViewport.ViewportHeight,
                "Medir DNS debe verse completo sin desplazamiento al llegar a RAM y red en 1000×680");
            var dnsLimit = Visuals<TextBlock>(ramView).Single(text => text.Text == "No cambia tus DNS ni mide el ping de juegos.");
            var limitBounds = dnsLimit.TransformToAncestor(ramViewport).TransformBounds(new Rect(dnsLimit.RenderSize));
            CheckUniversal(limitBounds.Top >= 0 && limitBounds.Bottom <= ramViewport.ViewportHeight,
                "Los límites de la medición DNS deben leerse completos junto a su acción en el tamaño mínimo");
            var pending = new DnsRowDisplay(new("Pendiente", "1.1.1.1", 0, 0, null, null, DateTimeOffset.Now));
            CheckUniversal(pending.Median == "Pendiente" && pending.Last == "Pendiente", "Una consulta no iniciada se distingue de una respuesta fallida");
            CheckUniversal(model.PageTitle == "RAM y red" && model.RamNetwork.Assessment.Pressure, "RAM y red utiliza datos del equipo y presión de memoria sostenida");
            memoryReadFails = true; model.RamNetwork.UpdateLive();
            CheckUniversal(!model.RamNetwork.Assessment.Pressure && model.RamNetwork.PhysicalMemory == "No disponible", "Una lectura de RAM fallida descarta las muestras anteriores");
            memoryReadFails = false; model.RamNetwork.UpdateLive();
            CheckUniversal(!window.Pages.Items.OfType<TabItem>().Any(item => item.Header?.ToString() is "Hardware" or "Gaming"), "Se retiran las pestañas duplicadas de Hardware y Gaming");
            await model.RamNetwork.MeasureAsync();
            CheckUniversal(model.RamNetwork.DnsRows.Single().Row.MedianMs == 12 && native.Mutations == 0, "La medición DNS presenta tiempos medidos sin modificar adaptadores ni Windows");
            CheckUniversal(model.Status == model.RamNetwork.DnsStatus, "El estado principal informa que la medición DNS terminó");
            var ramScroll = Visuals<ScrollViewer>(window).First(viewer => ReferenceEquals(viewer.DataContext, model.RamNetwork));
            ramScroll.ScrollToBottom();
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "dns-resultados-minimo.png"));
            CheckUniversal(Visuals<Button>(window).Any(button => button.IsVisible && button.Command == model.RamNetwork.MeasureCommand), "La vista de DNS mantiene disponible la medición real y sus resultados");
            model.ApplySnapshot(new SystemSnapshot(), isCurrent: true);
            model.SelectedPage = 0; await model.PcOptimization.FindProductsAsync();
            model.PcOptimization.SelectedProduct = model.PcOptimization.Products.Single(); await model.PcOptimization.LocateProductAsync();
            model.PcOptimization.SelectedExecutable = model.PcOptimization.Executables.Single();
            await model.PcOptimization.PrepareAsync();
            CheckUniversal(model.PcOptimization.Options.Any(row => row.Option.Id == "application-gpu"), "El producto elegido se localiza y su ajuste GPU se integra en Optimizar PC");
            CheckUniversal(!VisibleText(window, true).Any(text => text.Contains("Fortnite")), "El optimizador general no expone un perfil específico de Fortnite");
            await model.RamNetwork.MeasureAsync(); model.PcOptimization.RefreshTimerStatus();
            CheckUniversal(model.Status == model.RamNetwork.DnsStatus, "La actualización de estado del temporizador no reemplaza resultados de otras operaciones");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "producto-optimizador-minimo.png"));
            model.SelectedPage = 1;
            ramViewport.ScrollToTop();
            foreach (var theme in new[] { "Claro", "Oscuro", "Alto contraste" })
            {
                if (theme == "Alto contraste") ThemeManager.ApplyContrastFixture(); else ThemeManager.Apply(theme);
                await RenderFixtureWindowAsync(window, Path.Combine(directory, $"ram-red-{theme.ToLowerInvariant().Replace(' ', '-')}-minimo.png"));
                window.Width = 1240; window.Height = 820;
                await RenderFixtureWindowAsync(window, Path.Combine(directory, $"ram-red-{theme.ToLowerInvariant().Replace(' ', '-')}-amplio.png"));
                window.Width = 1000; window.Height = 680;
            }
            ThemeManager.Apply("Claro");
            var interpretation = (Expander)ramView.FindName("MemoryInterpretation");
            interpretation.IsExpanded = true;
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "ram-red-interpretacion.png"));
            CheckUniversal(VisibleText(ramView, true).Contains(model.RamNetwork.Assessment.Message), "El detalle conserva la recomendación completa y las advertencias de paginación");
            interpretation.IsExpanded = false;
            detailsFail = true; await model.RamNetwork.RefreshAsync();
            CheckUniversal(model.RamNetwork.HasMemoryError && !model.RamNetwork.HasMemory && !model.RamNetwork.HasProcesses && !model.HasError,
                "Un fallo de detalles retira las lecturas obsoletas y se explica dentro de RAM y red sin duplicar el aviso global");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "ram-red-error-memoria.png"));
            detailsFail = false; await model.RamNetwork.RefreshAsync();
            resolverFails = true; await model.RamNetwork.MeasureAsync();
            CheckUniversal(model.RamNetwork.HasDnsError && !model.RamNetwork.IsMeasuring && !model.RamNetwork.HasDnsRows && model.RamNetwork.MeasureCommand.CanExecute(null),
                "Fallar al consultar resolutores deja un error local, retira resultados anteriores y permite reintentar");
            CheckUniversal(!model.RamNetwork.DnsEmptyMessage.Contains("todavía"), "Un intento fallido no vuelve a presentarse como una primera medición sin iniciar");
            ramViewport.ScrollToBottom();
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "ram-red-error-dns.png"));
            resolverFails = false; blockDns = true;
            var dnsRun = model.RamNetwork.MeasureAsync(); await dnsStarted.Task;
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "ram-red-midiendo.png"));
            CheckUniversal(model.RamNetwork.IsMeasuring && !model.RamNetwork.MeasureCommand.CanExecute(null) && model.CancelCommand.CanExecute(null),
                "Medir muestra progreso, impide duplicar consultas y conserva Cancelar durante la operación");
            model.CancelCommand.Execute(null); await dnsRun;
            CheckUniversal(!model.RamNetwork.IsMeasuring && model.RamNetwork.DnsStatus.Contains("cancelada") && !model.RamNetwork.HasDnsError && native.Mutations == 0,
                "Cancelar DNS conserva el estado parcial sin elegir ganador ni modificar Windows");
            var partial = model.RamNetwork.DnsRows.Single();
            CheckUniversal(partial.Progress.Contains("1 fallo") && !partial.Progress.Contains("1 fallos"), "DNS usa el singular con un fallo");
            await RenderFixtureWindowAsync(window, Path.Combine(directory, "ram-red-cancelado.png"));
        }
        finally { window.Hide(); await model.PrepareToCloseAsync(); model.Dispose(); window.Close(); ThemeManager.Apply("Claro"); }
    }
    private sealed class FixtureProductCatalog : IInstalledProductCatalog
    {
        public Task<IReadOnlyList<InstalledProduct>> FindAsync(CancellationToken token = default) => Task.FromResult<IReadOnlyList<InstalledProduct>>([new("fixture", "Producto de prueba", "Prueba", null, Environment.ProcessPath!, "Prueba")]);
        public Task<IReadOnlyList<string>> LocateExecutablesAsync(InstalledProduct product, CancellationToken token = default) => Task.FromResult<IReadOnlyList<string>>([Environment.ProcessPath!]);
    }

    private static async Task RenderFixtureWindowAsync(Window window, string path)
    {
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (_, _) => rendered.TrySetResult();
        CompositionTarget.Rendering += handler;
        try { await rendered.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
        finally { CompositionTarget.Rendering -= handler; }
        window.UpdateLayout();
        var visual = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path); encoder.Save(stream);
    }

    private sealed class FixtureFortniteProfileService(string directory) : IFortniteProfileService
    {
        private readonly List<AppliedOptimization> changes = [];
        public int ApplyCalls { get; private set; }
        public bool PartialCancelNext { get; set; }
        public bool PartialResultNext { get; set; }
        public bool CancelResultNext { get; set; }
        public bool FailPreviewNext { get; set; }
        public bool MissingInstallation { get; set; }
        public bool BlockApply { get; set; }
        public bool BlockRecovery { get; set; }
        public string? LastRequestedDirectory { get; private set; }
        public TaskCompletionSource ApplyStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ApplyCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource RecoveryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRecovery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyCollection<string> AppliedIds { get; private set; } = [];
        public List<string> RestoredIds { get; } = [];
        public List<CancellationToken> PreviewTokens { get; } = [];
        public async Task<FortniteProfilePreview> PreviewAsync(string? installDirectory = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); PreviewTokens.Add(cancellationToken); LastRequestedDirectory = installDirectory;
            if (FailPreviewNext) { FailPreviewNext = false; throw new InvalidOperationException("Fixture detection permission denied"); }
            if (BlockRecovery && ApplyCalls > 0) { RecoveryStarted.TrySetResult(); await ReleaseRecovery.Task; }
            return new FortniteProfilePreview(new(!MissingInstallation, MissingInstallation ? null : installDirectory ?? Path.Combine(directory, "Fortnite fixture"), MissingInstallation ? null : Path.Combine(directory, "FortniteClient-Win64-Shipping.exe"), MissingInstallation ? null : Path.Combine(directory, "GameUserSettings.ini"), false, MissingInstallation ? "Fixture not installed" : "Fixture installation detected"),
                [new("winutil-game-mode", "Modo Juego", "Fixture system setting", true, true, "Disponible"), new("fortnite-settings", "Calidad competitiva", "Fixture lower visual quality", !MissingInstallation, true, "Juego cerrado"), new("gpu-preference", "GPU", "Fixture unavailable GPU", false, true, "No disponible"), new("animations", "Animaciones", "Fixture optional visuals", true, false, "Opcional")], changes.ToList(), "Fixture preview");
        }
        public async Task<FortniteProfileResult> ApplyAsync(IReadOnlyCollection<string> optionIds, IProgress<FortniteProfileProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); ApplyCalls++; AppliedIds = optionIds.ToArray();
            changes.Add(new(ApplyCalls == 1 ? "fixture-fortnite-change" : $"fixture-fortnite-change-{ApplyCalls}", "Fortnite fixture", DateTimeOffset.Now, true, "Fixture backup"));
            if (BlockApply)
            {
                progress?.Report(new(1, 2, "Fixture first change complete")); ApplyStarted.TrySetResult();
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { ApplyCancelled.TrySetResult(); throw; }
            }
            if (PartialCancelNext) { PartialCancelNext = false; throw new OperationCanceledException("Fixture parcialmente cancelado", cancellationToken); }
            if (PartialResultNext)
            {
                PartialResultNext = false;
                var cancelled = CancelResultNext; CancelResultNext = false;
                return new([new("winutil-game-mode", "Modo Juego", true, true, "Fixture applied"), new("fortnite-settings", "Calidad competitiva", false, false, "Fixture game opened; skipped")], cancelled, "Fixture partially applied");
            }
            progress?.Report(new(1, 1, "Fixture step"));
            return new(optionIds.Select(id => new FortniteProfileStepResult(id, "Fixture ajuste", true, true, "Fixture actual change")).ToList(), false, "Fixture profile applied");
        }
        public Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); RestoredIds.Add(changeId); changes.Clear();
            return Task.FromResult(new OptimizationOutcome(true, "Fixture restored", true));
        }
    }

    private sealed class FixtureOptimizationService : INativeOptimizationService
    {
        public FixtureOptimizationService(bool includeChrome = false)
        {
            if (includeChrome) toggles.Add(new("chrome-background", "Reducir procesos de Chrome al cerrarlo", "Fixture de la política de segundo plano; selección manual.", false, "Disponible", Category: "Segundo plano", UsesBackupRestore: true));
        }
        internal WindowsUpdateHealth UpdateHealth = WindowsUpdateHealth.Unknown("Estado simulado sin consultar.");
        internal int UpdateResumeCalls;
        internal string? AppliedGpuPath;
        public Task<WindowsUpdateHealth> InspectWindowsUpdateAsync(CancellationToken cancellationToken = default) => Task.FromResult(UpdateHealth);
        public Task<OptimizationOutcome> ResumeWindowsUpdateAsync(CancellationToken cancellationToken = default)
        {
            UpdateResumeCalls++; var previous = UpdateHealth;
            return Change("Windows Update simulado", () => UpdateHealth = new(true, false, null, true, true, false, "Servicio en ejecución y sin pausa."), () => UpdateHealth = previous, cancellationToken);
        }
        public Task<OptimizationOutcome> SetApplicationGpuPreferenceAsync(string executablePath, CancellationToken cancellationToken = default)
        {
            var previous = AppliedGpuPath;
            return Change("GPU de aplicación simulada", () => AppliedGpuPath = executablePath, () => AppliedGpuPath = previous, cancellationToken);
        }
        public bool? ReadApplicationGpuPreference(string executablePath) => AppliedGpuPath == executablePath ? true : null;
        public Task<OptimizationOutcome> DisableStartupCheckedAsync(string id, string expectedCommand, CancellationToken cancellationToken = default)
        {
            var entry = startup.FirstOrDefault(item => item.Id == id);
            return entry is null || !entry.Enabled || entry.Command != expectedCommand
                ? Task.FromResult(new OptimizationOutcome(false, "La entrada simulada cambió."))
                : SetStartupEnabledAsync(id, false, cancellationToken);
        }
        private readonly List<ToggleSetting> toggles = [new("animations", "Animaciones", "Fixture animations", true, "Fixture"), new("game-mode", "Modo Juego", "Fixture Game Mode", false, "Fixture"), new("xbox-captures", "Capturas Xbox", "Fixture Xbox recording", true, "Fixture")];
        private readonly List<PowerScheme> power = [new("balanced", "Equilibrado", true), new("performance", "Rendimiento fixture", false)];
        private readonly List<Silen.Windows.StartupEntry> startup = [new("user-run", "Fixture launcher", "fixture.exe", true, "Usuario actual")];
        private readonly List<AppliedOptimization> changes = [];
        private readonly Dictionary<string, Action> restores = [];
        public int Mutations { get; private set; }
        public bool HideBackups { get; set; }
        public bool UndoConflict { get; set; }
        public bool UndoPartial { get; set; }
        public int UndoCalls { get; private set; }
        public bool FailNext { get; set; }
        public bool PartialCancelNext { get; set; }
        public bool CancelPreviewNext { get; set; }
        public bool BlockInspection { get; set; }
        public TaskCompletionSource InspectionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource InspectionCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseInspection { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyCollection<string> CleanedIds { get; private set; } = [];
        public async Task<OptimizationOverview> InspectAsync(CancellationToken cancellationToken = default)
        {
            if (BlockInspection)
            {
                InspectionStarted.TrySetResult();
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { InspectionCancelled.TrySetResult(); await ReleaseInspection.Task; throw; }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new(toggles.ToList(), power.ToList(), startup.ToList(), HideBackups ? [] : changes.ToList());
        }
        private Task<OptimizationOutcome> Change(string title, Action apply, Action restore, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (FailNext) { FailNext = false; return Task.FromResult(new OptimizationOutcome(false, "Fixture permission denied")); }
            apply(); Mutations++;
            var id = Guid.NewGuid().ToString("N");
            changes.Add(new(id, title, DateTimeOffset.Now, true, "Fixture saved previous state")); restores.Add(id, restore);
            if (PartialCancelNext) { PartialCancelNext = false; throw new OperationCanceledException(token); }
            return Task.FromResult(new OptimizationOutcome(true, "Fixture change applied and checked", true));
        }
        public Task<OptimizationOutcome> SetToggleAsync(string id, bool enabled, CancellationToken cancellationToken = default)
        {
            var index = toggles.FindIndex(t => t.Id == id); var previous = toggles[index];
            return Change(previous.Name, () => toggles[index] = previous with { Enabled = enabled }, () => toggles[index] = previous, cancellationToken);
        }
        public Task<OptimizationOutcome> SetPowerSchemeAsync(string id, CancellationToken cancellationToken = default)
        {
            var previous = power.ToList();
            return Change("Fixture power", () => { for (var i = 0; i < power.Count; i++) power[i] = power[i] with { Active = power[i].Id == id }; }, () => { power.Clear(); power.AddRange(previous); }, cancellationToken);
        }
        public Task<OptimizationOutcome> SetStartupEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default)
        {
            var index = startup.FindIndex(s => s.Id == id); var previous = startup[index];
            return Change(previous.Name, () => startup[index] = previous with { Enabled = enabled }, () => startup[index] = previous, cancellationToken);
        }
        public Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default)
        {
            UndoCalls++;
            if (UndoConflict) return Task.FromResult(new OptimizationOutcome(false, "Windows u otra aplicación cambió esta preferencia. Se conservó el cambio externo."));
            if (UndoPartial) { Mutations++; return Task.FromResult(new OptimizationOutcome(false, "No se completó la restauración; el respaldo sigue disponible.", true)); }
            cancellationToken.ThrowIfCancellationRequested(); restores[changeId](); restores.Remove(changeId); changes.RemoveAll(c => c.Id == changeId); Mutations++;
            return Task.FromResult(new OptimizationOutcome(true, "Fixture previous state restored", true));
        }
        public Task<TemporaryCleanupPreview> PreviewTempAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CancelPreviewNext) { CancelPreviewNext = false; throw new OperationCanceledException(cancellationToken); }
            return Task.FromResult(new TemporaryCleanupPreview([new("first", "fixture-first.tmp", 1024), new("second", "fixture-second.tmp", 2048)], 3072, false, "Fixture preview"));
        }
        public Task<TemporaryCleanupResult> CleanTempAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); CleanedIds = ids.ToArray(); Mutations++;
            return Task.FromResult(new TemporaryCleanupResult(1, 0, 2048, "Fixture selected file removed"));
        }
        public List<CleanupRecoveryItem> RecoveryItems { get; } = [];
        public IReadOnlyCollection<string> RestoredIds { get; private set; } = [];
        public IReadOnlyCollection<string> PurgedIds { get; private set; } = [];
        public Task<IReadOnlyList<CleanupRecoveryItem>> ListRecoveryAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CleanupRecoveryItem>>(RecoveryItems.ToArray());
        public Task<TemporaryCleanupResult> RestoreRecoveryAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); RestoredIds = ids.ToArray(); RecoveryItems.RemoveAll(item => ids.Contains(item.Id));
            return Task.FromResult(new TemporaryCleanupResult(ids.Count, 0, 0, "Fixture restored"));
        }
        public Task<TemporaryCleanupResult> PurgeRecoveryAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); PurgedIds = ids.ToArray(); RecoveryItems.RemoveAll(item => ids.Contains(item.Id));
            return Task.FromResult(new TemporaryCleanupResult(ids.Count, 0, 2048, "Fixture purged"));
        }
    }
}
