using Silen.Core;
using Silen.Windows;
using Silen.Gaming;
using System.IO;
using System.Diagnostics;

namespace Silen.App;


internal sealed class ViewModelServices
{
    public Func<string, string, bool> ConfirmAction { get; init; } = (_, _) => false;
    public Func<IRestorePointService> CreateRestorePoint { get; init; } = () => new SystemRestoreClient((_, _) => Task.FromResult(new SystemActionResponse(new(false, "Punto no disponible en este contexto."))));
    public Func<RestorePointResult, bool> ConfirmRestoreFallback { get; init; } = _ => false;
    public Action OpenSystemProtection { get; init; } = Elevation.OpenSystemProtection;
    public Action RequestRestart { get; init; } = () => { };
    public bool UsesSystemExecutor { get; init; }
    public Action<string> ApplyTheme { get; init; } = _ => { };
    public Func<IBloomTools, IBloomSession>? CreateBloomSession { get; init; }
    public Action<string> OpenBloomProviderPage { get; init; } = url => WindowsActions.OpenTrustedAiPage(url);
    public Func<IApplicationTimerService> CreateApplicationTimer { get; init; } = () => new InactiveApplicationTimerService();
    public Func<IUserPreferencesStore> CreatePreferences { get; init; } = () => new MemoryUserPreferencesStore();
    public IAppUpdateService AppUpdates { get; init; } = new AppUpdateService();
    public Version InstalledAppVersion { get; init; } = typeof(MainViewModel).Assembly.GetName().Version ?? new Version(1, 1, 0, 0);
    public Func<PreparedAppUpdate, ProcessStartInfo> PrepareAppUpdate { get; init; } = PortableAppUpdater.CreateStartInfo;
    public Action<ProcessStartInfo> LaunchAppUpdate { get; init; } = start =>
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Windows no pudo abrir el instalador de Silen.");
    };
    public Func<IProgress<string>?, CancellationToken, IProgress<ScanProgress>?, Task<SystemSnapshot>> Collect { get; init; } =
        (progress, token, detailed) => new WindowsCollector().CollectAsync(progress, token, detailed);
    public Func<CancellationToken, Task<DriverUpdateResult>> FindUpdates { get; init; } =
        token => new DriverUpdateService().FindUpdatesAsync(token);
    public Func<IReadOnlyList<DriverUpdateInfo>, CancellationToken, Task<DriverDownloadResult>> DownloadDriverUpdates { get; init; } =
        (updates, token) => new DriverDownloadService().DownloadAsync(updates, token);
    public Func<MachineInfo, bool, CancellationToken, Task<OemSupportResult>> FindOem { get; init; } =
        (machine, serial, token) => new OemDriverService().FindAsync(machine, serial, token);
    public Action<ActionKind, string?> Open { get; init; } = WindowsActions.Open;
    public Func<INativeOptimizationService> CreateNativeTools { get; init; } = () => new NativeOptimizationService();
    public Func<IInstalledProductCatalog> CreateInstalledProducts { get; init; } = () => new InstalledProductCatalog();
    public Func<bool?, MemoryObservation?> ReadMemory { get; init; } = MemoryDiagnostics.ReadFast;
    public Func<CancellationToken, Task<bool?>> ReadAutomaticPagefile { get; init; } = MemoryDiagnostics.ReadAutomaticPagefileAsync;
    public Func<CancellationToken, Task<IReadOnlyList<Silen.Core.ProcessInfo>>> ReadMemoryProcesses { get; init; } = MemoryDiagnostics.ReadProcessesAsync;
    public Func<IReadOnlyList<DnsResolver>> GetDnsResolvers { get; init; } = DnsBenchmarkService.GetResolvers;
    public Func<IReadOnlyList<DnsResolver>, IProgress<DnsBenchmarkRow>?, CancellationToken, Task<IReadOnlyList<DnsBenchmarkRow>>> MeasureDns { get; init; } = (resolvers, progress, token) => new DnsBenchmarkService().MeasureAsync(resolvers, progress, token);
    public Func<IBloomKeyStore> CreateBloomKeys { get; init; } = () => new BloomKeyStore();
    public Func<IBloomClient> CreateBloomClient { get; init; } = () => new BloomGeminiClient();
    public Func<INativeOptimizationService, Func<CancellationToken, Task<SystemSnapshot>>, IBloomTools> CreateBloomTools { get; init; } = (native, collect) => new BloomWindowsTools(native, collect);
    public Action OpenBloomKeyPage { get; init; } = () =>
    {
        using var process = Process.Start(new ProcessStartInfo("https://aistudio.google.com/app/apikey") { UseShellExecute = true });
    };
    public Func<string?> ChooseGameExecutable { get; init; } = () =>
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Elegir el ejecutable de un juego o aplicación", Filter = "Aplicaciones (*.exe)|*.exe", CheckFileExists = true, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    };
    public Func<INativeOptimizationService, IPcOptimizationService> CreatePcOptimization { get; init; } = native => new PcOptimizationService(native);
    public Func<IReadOnlyList<PcOptimizationOption>, bool> ConfirmPcOptimization { get; init; } = OptimizationConfirmation.ConfirmPc;
    public Func<IFortniteProfileService> CreateFortniteProfile { get; init; } = () => new FortniteProfileService();
    public Func<bool> ConfirmOptimization { get; init; } = OptimizationConfirmation.Confirm;
    public Func<string?> ChooseFortniteFolder { get; init; } = () =>
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Seleccionar carpeta de instalación de Fortnite", Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    };
    public Func<string> ResolvePresentMon { get; init; } = () => BundledPresentMon.Resolve();
    public Func<string, int, int, string, CancellationToken, Task<GamingSession>> Capture { get; init; } =
        (executable, pid, duration, path, token) => new GamingCapture().CaptureAsync(executable, pid, duration, path, token);
    public string CaptureDirectory { get; init; } = Path.Combine(AppDataPaths.Root, "Captures");
    public Func<bool> IsElevated { get; init; } = Elevation.IsElevated;
    public Action OpenElevated { get; init; } = Elevation.OpenElevated;
    public Action OpenElevatedForUpdates { get; init; } = Elevation.OpenElevatedDrivers;
}
