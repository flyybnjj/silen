using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Silen.Core;
using Silen.Windows;

namespace Silen.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        var normalRelaunch = e.Args.Contains("--normal-ui", StringComparer.Ordinal);
        var startupArgs = e.Args.Where(arg => arg != "--normal-ui").ToArray();
        AppUpdateStartupTicket? updateStartup = null;
        base.OnStartup(e);
        // Program despacha el ayudante antes de WPF; rechazar aquí mezclas de argumentos.
        if (startupArgs.FirstOrDefault() == SystemActionHost.Argument)
        {
            Shutdown(2);
            return;
        }
        if (Elevation.IsElevated() && startupArgs.FirstOrDefault() != "--apply-update")
        {
            var restarted = !normalRelaunch && (startupArgs.Length == 0 || startupArgs is ["--drivers"]) && Elevation.TryOpenWithoutAdministrator(startupArgs is ["--drivers"]);
            if (!restarted) MessageBox.Show("Abre Silen sin «Ejecutar como administrador». Las operaciones del sistema solicitarán su propia autorización de Windows.", "Silen", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }
        try
        {
            using var startupDiagnostics = startupArgs.Length == 0 ? StartupDiagnostics.FromEnvironment() : null;
            if (startupArgs.FirstOrDefault() == AppUpdateStartup.Argument)
            {
                updateStartup = AppUpdateStartup.Parse(startupArgs);
                startupArgs = [];
            }
            if (startupArgs is ["--performance-test", var performanceDirectory])
            {
                await PerformanceProbe.RunAsync(Path.GetFullPath(performanceDirectory));
                Shutdown(0); return;
            }
            if (startupArgs.FirstOrDefault() == "--apply-update")
            {
                try
                {
                    if (startupArgs.Length != 2) throw new ArgumentException("El instalador necesita un trabajo de actualización válido.");
                    await PortableAppUpdater.ApplyFromJobAsync(startupArgs[1]);
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Silen no pudo aplicar la actualización. {ex.Message}", "Error de actualización de Silen", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                }
                return;
            }
            var driverMode = startupArgs.Length == 1 && startupArgs[0] == "--drivers";
            if (startupArgs.Length > 0 && !driverMode)
            {
                if (startupArgs.Length != 2 || startupArgs[0] is not ("--scan" or "--render" or "--smoke-test" or "--interaction-test" or "--motion-preview"))
                    throw new ArgumentException("Uso: Silen.exe --scan <informe.json> | --render <vista.png> | --smoke-test <directorio> | --interaction-test <directorio> | --motion-preview <directorio>");
                if (startupArgs[0] == "--motion-preview")
                {
                    await RenderMotionPreviewAsync(Path.GetFullPath(startupArgs[1]));
                    Shutdown(0); return;
                }
                if (startupArgs[0] == "--scan")
                {
                    var result = await new WindowsCollector().CollectAsync(null, CancellationToken.None);
                    await new ReportStore().SaveAsync(result);
                    await WriteJsonAsync(Path.GetFullPath(startupArgs[1]), ReportPrivacy.ForSharing(result));
                    Shutdown(0); return;
                }
                if (startupArgs[0] == "--interaction-test")
                {
                    var directory = Path.GetFullPath(startupArgs[1]);
                    var checks = await InteractionSmokeChecks.RunAsync(Path.Combine(directory, "interaction-fixtures"), Dispatcher);
                    await WriteJsonAsync(Path.Combine(directory, "interaction-checks.json"), new { Success = true, FixtureOnly = true, Checks = checks, ExternalToolsOpened = false, SystemSettingsApplied = false, CapturedGaming = false, ViewsRendered = false });
                    Shutdown(0); return;
                }
            }

            var model = new MainViewModel(startupArgs.FirstOrDefault() == "--smoke-test" ? Path.Combine(Path.GetFullPath(startupArgs[1]), "history") : null);
            var automaticStart = startupArgs.Length == 0 || driverMode;
            Task? startupUpdate = null;
            Task initialization;
            if (automaticStart)
            {
                model.BeginStartupPresentation();
                using (var splash = new BloomSplashWindow(model.CancelStartup))
                {
                    splash.Show();
                    startupUpdate = StartupDiagnostics.TrackAsync("GitHub", model.StartAppUpdateCheckAsync);
                    initialization = model.InitializeAsync(autoScan: true);
                    await model.StartupPresentationReady;
                    if (model.StartupCancelled)
                    {
                        await initialization; await model.PrepareToCloseAsync(); Shutdown(0); return;
                    }
                }
            }
            else initialization = model.InitializeAsync(autoScan: false);
            var window = new MainWindow(model, animateStartup: automaticStart);
            MainWindow = window;
            window.Closed += (_, _) => { if (automaticStart) Shutdown(0); };
            window.Show();
            startupDiagnostics?.Observe(model, window);
            await initialization;
            if (startupDiagnostics is not null && (startupArgs.Length == 0 || driverMode))
            {
                if (startupUpdate is not null) await startupUpdate;
                await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                await startupDiagnostics.SaveAsync(model);
                await model.PrepareToCloseAsync(); window.Close(); Shutdown(0); return;
            }
            if (updateStartup is not null)
            {
                await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                if (!window.IsLoaded || !window.IsVisible) throw new InvalidOperationException("La ventana no completó el arranque.");
                await AppUpdateStartup.ReportReadyAsync(updateStartup);
            }
            if (startupArgs.Length == 0 || driverMode)
            { if (driverMode) model.SelectedPage = 2; ShutdownMode = ShutdownMode.OnMainWindowClose; return; }
            Task? liveRender = null;
            System.ComponentModel.PropertyChangedEventHandler? captureLive = null;
            if (startupArgs[0] == "--smoke-test")
            {
                var renderDirectory = Path.GetFullPath(startupArgs[1]);
                await RenderAsync(window, Path.Combine(renderDirectory, "empty.png"));
                model.SelectedPage = 2;
                await RenderAsync(window, Path.Combine(renderDirectory, "drivers-empty.png"));
                model.SelectedPage = 0;
                window.Width = window.MinWidth; window.Height = window.MinHeight;
                await RenderAsync(window, Path.Combine(renderDirectory, "empty-minimum.png"));
                window.Width = 1240; window.Height = 820;
                await SettleLayoutAsync(window);
                captureLive = (_, _) =>
                {
                    if (liveRender is null && model.IsScanning && model.CpuTrace.Count >= 2)
                        liveRender = RenderAsync(window, Path.Combine(renderDirectory, "scan-live.png"));
                };
                model.PropertyChanged += captureLive;
            }
            try
            {
                if (!await model.ScanAsync() || model.Snapshot is null) throw new InvalidOperationException("El escaneo de verificación no terminó: " + model.Error);
                if (liveRender is not null) await liveRender;
            }
            finally { if (captureLive is not null) model.PropertyChanged -= captureLive; }
            await SettleLayoutAsync(window);
            if (startupArgs[0] == "--render")
            {
                await RenderAsync(window, Path.GetFullPath(startupArgs[1]));
            }
            else
            {
                var directory = Path.GetFullPath(startupArgs[1]);
                Directory.CreateDirectory(directory);
                var interactionChecks = await InteractionSmokeChecks.RunAsync(Path.Combine(directory, "interaction-fixtures"), window.Dispatcher);
                await WriteJsonAsync(Path.Combine(directory, "interaction-checks.json"), new { Success = true, FixtureOnly = true, Checks = interactionChecks, ExternalToolsOpened = false, SystemSettingsApplied = false, CapturedGaming = false });
                await WriteJsonAsync(Path.Combine(directory, "snapshot.json"), ReportPrivacy.ForSharing(model.Snapshot));
                await File.WriteAllTextAsync(Path.Combine(directory, "report.md"), ReportFormatter.ToMarkdown(model.Snapshot, RecommendationEngine.Analyze(model.Snapshot)));
                var pages = new[] { "summary", "ram-network", "drivers", "improvements", "history", "bloom" };
                var initialActual = new { Width = window.ActualWidth, Height = window.ActualHeight };
                for (var i = 0; i < pages.Length; i++)
                {
                    model.SelectedPage = i;
                    await SettleLayoutAsync(window);
                    await RenderAsync(window, Path.Combine(directory, pages[i] + ".png"));
                }
                await RenderDeepViewsAsync(window, model, directory, "");
                window.Width = window.MinWidth; window.Height = window.MinHeight;
                for (var i = 0; i < pages.Length; i++)
                {
                    model.SelectedPage = i;
                    await SettleLayoutAsync(window);
                    await RenderAsync(window, Path.Combine(directory, pages[i] + "-minimum.png"));
                }
                await RenderDeepViewsAsync(window, model, directory, "-minimum");
                // Variantes de Resumen para verificar contraste sin cambiar preferencias guardadas.
                model.SelectedPage = 0;
                try
                {
                    ThemeManager.Apply("Oscuro"); window.Width = 1240; window.Height = 820;
                    await SettleLayoutAsync(window);
                    await RenderAsync(window, Path.Combine(directory, "summary-dark.png"));
                    window.Width = window.MinWidth; window.Height = window.MinHeight;
                    await SettleLayoutAsync(window);
                    await RenderAsync(window, Path.Combine(directory, "summary-dark-minimum.png"));
                }
                finally { ThemeManager.Apply(model.Settings.SelectedTheme); }
                var confirmation = new OptimizationConfirmation { Owner = window, ShowActivated = false };
                confirmation.Show();
                await RenderAsync(confirmation, Path.Combine(directory, "optimization-confirmation.png"));
                confirmation.Close();
                await WriteJsonAsync(Path.Combine(directory, "status.json"), new
                {
                    Success = true, model.Snapshot.CapturedAt, model.Snapshot.Id,
                    Samples = model.Snapshot.Samples.Count, Drivers = model.Snapshot.Drivers.Count,
                    Findings = model.Findings.Count, Notes = model.Snapshot.Notes,
                    ViewsRendered = pages, InitialSize = new { Width = 1240, Height = 820 },
                    MinimumSize = new { Width = window.MinWidth, Height = window.MinHeight },
                    InitialActualSize = initialActual, MinimumActualSize = new { Width = window.ActualWidth, Height = window.ActualHeight },
                    CapturedGaming = false, AppliedSystemChanges = false,
                    InteractionChecks = interactionChecks.Count, ModelLookup = model.OemStatus, ModelPackages = model.OemPackages.Count, CaptureProcesses = model.CaptureProcesses.Count,
                    SerialLookupRequested = false, LiveScanRendered = liveRender is not null,
                    UniversalOptimizationReady = window.Pages.Items.Count == 6 && window.FindName("FortniteProfileSection") is null, FortniteInstalled = model.Fortnite.IsInstalled,
                    FortniteDetectedDirectory = model.Fortnite.IsInstalled ? model.Fortnite.InstallPath : null,
                    FortniteAvailableOptions = model.Fortnite.Options.Count(option => option.Option.Available),
                    FortniteUnavailableOptions = model.Fortnite.Options.Where(option => !option.Option.Available)
                        .Select(option => new { option.Option.Id, option.Option.Status }).ToArray()
                });
            }
            model.Dispose(); window.Close(); Shutdown(0);
        }
        catch (Exception ex)
        {
            try
            {
                var directory = AppDataPaths.Root;
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "last-error.txt"), $"{DateTimeOffset.Now:O}\n{ex}");
                if (startupArgs.Length == 2 && startupArgs[0] is "--smoke-test" or "--interaction-test" or "--motion-preview")
                {
                    Directory.CreateDirectory(Path.GetFullPath(startupArgs[1]));
                    await WriteJsonAsync(Path.Combine(Path.GetFullPath(startupArgs[1]), "status.json"), new { Success = false, Error = ex.Message });
                }
            }
            catch {  }
            if (startupArgs.Length == 0) MessageBox.Show($"Silen no pudo iniciar. {ex.Message}", "Silen", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(startupArgs.Length > 0 && ex is ArgumentException ? 2 : 1);
        }
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, ReportStore.JsonOptions));
    }
    private static async Task SettleLayoutAsync(Window window, int settleMilliseconds = Motion.SettleMilliseconds)
    {
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Task.Delay(settleMilliseconds);


        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (_, _) => rendered.TrySetResult();
        CompositionTarget.Rendering += handler;
        try { await rendered.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
        finally { CompositionTarget.Rendering -= handler; }
        window.UpdateLayout();
    }
    private static async Task RenderAsync(Window window, string path, int settleMilliseconds = Motion.SettleMilliseconds)
    {
        await SettleLayoutAsync(window, settleMilliseconds);
        var visual = (FrameworkElement)window.Content;
        var width = Math.Max(1, (int)Math.Ceiling(visual.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(visual.ActualHeight));
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        image.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static async Task RenderDeepViewsAsync(MainWindow window, MainViewModel model, string directory, string suffix)
    {
        foreach (var (page, anchor, name) in new[] { (2, "SelectedOemPackageSection", "drivers-package"), (3, "TemporarySection", "improvements-cleanup"), (3, "UndoSection", "improvements-undo") })
        {
            model.SelectedPage = page;
            if (page == 2) model.SelectedOemPackage = model.OemPackages.FirstOrDefault();
            await SettleLayoutAsync(window);
            if (window.Pages.SelectedContent is System.Windows.Controls.ScrollViewer viewer)
            {
                if (window.FindName(anchor) is not FrameworkElement { IsVisible: true } section)
                    throw new InvalidOperationException("No se pudo verificar la sección " + anchor + ".");
                viewer.ScrollToVerticalOffset(Math.Max(0, viewer.VerticalOffset + section.TranslatePoint(new Point(0, 0), viewer).Y));
                await RenderAsync(window, Path.Combine(directory, name + suffix + ".png"));
                viewer.ScrollToTop();
                await SettleLayoutAsync(window);
            }
        }
        model.SelectedOemPackage = null;
    }

    private static async Task RenderMotionPreviewAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var frames = Path.Combine(directory, "frames");
        Directory.CreateDirectory(frames);
        var ambientFrames = Path.Combine(directory, "ambient-frames");
        Directory.CreateDirectory(ambientFrames);
        using var model = new MainViewModel(Path.Combine(directory, "history"));
        var window = new MainWindow(model) { ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
        window.Show();
        await SettleLayoutAsync(window);
        foreach (var (phase, name) in new[] { (0d, "bloom-sharp"), (.25, "bloom-quarter"), (.5, "bloom-half"), (.75, "bloom-three-quarters") })
        {
            window.CaptureArtworkAt(phase);
            await RenderAsync(window, Path.Combine(directory, name + ".png"));
        }
        for (var i = 0; i < 84; i++)
        {
            window.CaptureMotionAt(i / 24d);
            await RenderAsync(window, Path.Combine(frames, $"frame-{i:D3}.png"), 32);
        }
        var ambientFrameCount = (int)(RibbonMotionTimeline.AmbientPeriodSeconds * 24);
        for (var i = 0; i < ambientFrameCount; i++)
        {
            window.CaptureMotionAt(RibbonMotionTimeline.AmbientPeriodSeconds + i / 24d);
            await RenderAsync(window, Path.Combine(ambientFrames, $"frame-{i:D3}.png"), 32);
        }
        window.CaptureMotionAt(RibbonMotionTimeline.AmbientPeriodSeconds);
        await RenderAsync(window, Path.Combine(directory, "background.png"));
        var renderer = window.RibbonArtwork;
        var result = new
        {
            Success = renderer.IsFrameFinite, renderer.VertexCount, renderer.TriangleCount,
            Frames = 84, FramesPerSecond = 24, TimelineSeconds = 3.5,
            IntroSeconds = RibbonMotionTimeline.IntroDuration,
            AmbientPeriodSeconds = RibbonMotionTimeline.AmbientPeriodSeconds,
            AmbientFrames = ambientFrameCount, WingbeatSeconds = RibbonMotionTimeline.AmbientPeriodSeconds / 2,
            Mascot = "BLOOM", LetterTransformation = false,
            BlurAppliedToArtworkOnly = true, FixtureOnly = true, SystemSettingsApplied = false,
            window.MotionDiagnostics.MaximumRenderMilliseconds
        };
        await WriteJsonAsync(Path.Combine(directory, "motion-status.json"), result);
        await WriteJsonAsync(Path.Combine(directory, "status.json"), result);
        window.Close();
    }
}
