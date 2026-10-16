namespace Silen.Core;

public sealed record SystemSnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;
    public MachineInfo Machine { get; init; } = new();
    public WindowsUpdateHealth? WindowsUpdate { get; init; }
    public List<MetricSample> Samples { get; init; } = [];
    public List<StorageInfo> Drives { get; init; } = [];
    public List<DriverInfo> Drivers { get; init; } = [];
    public List<StartupEntry> Startup { get; init; } = [];
    public List<ProcessInfo> Processes { get; init; } = [];
    public List<NetworkInfo> Networks { get; init; } = [];
    public List<string> Notes { get; init; } = [];
}

public sealed record MachineInfo
{
    public string Manufacturer { get; init; } = "";
    public string Model { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string SystemSku { get; init; } = "";
    public DeviceKind DeviceKind { get; init; }
    public string DeviceTypeEvidence { get; init; } = "";
    public string OsName { get; init; } = "";
    public string OsVersion { get; init; } = "";
    public string CpuName { get; init; } = "";
    public string GpuName { get; init; } = "";
    public int LogicalCores { get; init; }
    public double TotalMemoryGb { get; init; }
    public double UptimeHours { get; init; }
    public string PowerPlanGuid { get; init; } = "";
    public string PowerPlanName { get; init; } = "";
    public bool IsLaptop { get; init; }
    public bool OnBattery { get; init; }
    public bool IsAdministrator { get; init; }
}

public enum DeviceKind { Unknown, Laptop, Desktop }
public sealed record ScanProgress(string Stage, string Detail, int CompletedSteps, int TotalSteps, MetricSample? Sample = null);
public sealed record OemDriverPackage(string Name, string Version, string Category, string ReleaseDate, string OperatingSystem, string? DownloadUrl, string? DetailUrl);
public sealed record OemSupportResult(bool Success, string Provider, string DeviceName, string SupportUrl, List<OemDriverPackage> Packages, string Message, bool ModelMatched, string SerialDisplay = "");

public sealed record MetricSample
{
    public DateTimeOffset At { get; init; } = DateTimeOffset.Now;
    public double? CpuPercent { get; init; }
    public double? MemoryPercent { get; init; }
    public double? GpuPercent { get; init; }
    public double? CpuTemperature { get; init; }
    public double? GpuTemperature { get; init; }
}

public sealed record StorageInfo
{
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public string FileSystem { get; init; } = "";
    public string Kind { get; init; } = "";
    public double TotalGb { get; init; }
    public double FreeGb { get; init; }
    public double? FreePercent => TotalGb > 0 ? FreeGb / TotalGb * 100 : null;
}

public sealed record DriverInfo
{
    public string DeviceName { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Provider { get; init; } = "";
    public string Version { get; init; } = "";
    public string Date { get; init; } = "";
    public bool? Signed { get; init; }
    public int? ProblemCode { get; init; }
    public string DeviceClass { get; init; } = "";
    public string HardwareId { get; init; } = "";
}

public sealed record StartupEntry(string Name, string Command, string Source);
public sealed record ProcessInfo(string Name, int Id, double MemoryMb);
public sealed record NetworkInfo(string Name, string Status, long SpeedMbps);
public enum Severity { Info, Suggestion, Warning, Critical }
public enum ActionKind { None, OpenStartup, OpenStorage, OpenPower, OpenWindowsUpdate, OpenOptionalDrivers, OpenGraphics, OpenGameMode, OpenManufacturer, OpenTaskManager }

public sealed record Finding(string Id, string Title, string Description, string Evidence, Severity Severity, string Category, ActionKind Action = ActionKind.None, string? ActionUrl = null);
public sealed record DriverUpdateInfo(string Title, string Manufacturer, string DeviceClass, string Version, string Date,
    string UpdateId = "", int Revision = 0, bool Downloaded = false, bool EulaAccepted = true)
{
    public string DownloadState => Downloaded ? "Descargada" : EulaAccepted ? "Disponible para descargar" : "Requiere aceptar su licencia en Windows Update";
}
public sealed record DriverUpdateResult(bool Success, List<DriverUpdateInfo> Updates, string Message, WindowsUpdateHealth? WindowsUpdate = null);
public sealed record DriverDownloadResult(bool Success, List<string> DownloadedIds, string Message);
public sealed record ActionRecord(DateTimeOffset At, string FindingId, string Title, string Outcome);
public sealed record SavedReport(string Path, DateTimeOffset CapturedAt, string CpuName, int FindingCount);

public sealed record GamingSession
{
    public string SourceFile { get; init; } = "";
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;
    public string ProcessName { get; init; } = "";
    public int FrameCount { get; init; }
    public double AverageFps { get; init; }
    public double AverageFrameTimeMs { get; init; }
    public double P95FrameTimeMs { get; init; }
    public double P99FrameTimeMs { get; init; }
    public double OnePercentLowFps { get; init; }
    public int IgnoredRows { get; init; }
    public string TimingColumn { get; init; } = "";
    public List<double> FrameTimesMs { get; init; } = [];
}
