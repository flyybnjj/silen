using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Principal;
using Silen.Core;
using Microsoft.Win32;
using InventoryStartupEntry = Silen.Core.StartupEntry;

namespace Silen.Windows;

public sealed class WindowsCollector
{
    public Task<SystemSnapshot> CollectAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default, IProgress<ScanProgress>? detailedProgress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => Collect(progress, cancellationToken, detailedProgress), cancellationToken);
    }

    private static SystemSnapshot Collect(IProgress<string>? progress, CancellationToken token, IProgress<ScanProgress>? detailedProgress)
    {
        var snapshot = new SystemSnapshot();
        var updateHealth = new NativeOptimizationService().ReadWindowsUpdateStatus();
        snapshot = snapshot with { WindowsUpdate = updateHealth };
        if (!updateHealth.CanSearch) snapshot.Notes.Add(updateHealth.Message);
        progress?.Report("Leyendo sistema, CPU y gráficos…");
        snapshot = snapshot with { Machine = Machine(snapshot.Notes, token) };
        detailedProgress?.Report(new ScanProgress("Equipo", "Sistema, CPU, gráficos e identidad del equipo leídos.", 1, 14));
        token.ThrowIfCancellationRequested();
        progress?.Report("Leyendo unidades y dispositivos…");
        snapshot.Drives.AddRange(Drives(snapshot.Notes, token));
        snapshot.Drivers.AddRange(Drivers(snapshot.Notes, token));
        detailedProgress?.Report(new ScanProgress("Dispositivos", "Unidades y controladores instalados leídos.", 2, 14));
        token.ThrowIfCancellationRequested();
        progress?.Report("Leyendo inicio, procesos y red…");
        snapshot.Startup.AddRange(Startup(snapshot.Notes, token));
        TryProvider("Procesos", snapshot.Notes, () =>
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    token.ThrowIfCancellationRequested();
                    try { snapshot.Processes.Add(new ProcessInfo(process.ProcessName, process.Id, process.WorkingSet64 / 1048576d)); }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                }
            }
            snapshot.Processes.Sort((a, b) => b.MemoryMb.CompareTo(a.MemoryMb));
            if (snapshot.Processes.Count > 20) snapshot.Processes.RemoveRange(20, snapshot.Processes.Count - 20);
        });
        TryProvider("Red", snapshot.Notes, () =>
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            {
                token.ThrowIfCancellationRequested();
                snapshot.Networks.Add(new NetworkInfo(adapter.Description, adapter.OperationalStatus.ToString(), Math.Max(0, adapter.Speed / 1_000_000)));
            }
        });

        detailedProgress?.Report(new ScanProgress("Actividad", "Inicio, procesos y adaptadores de red leídos.", 3, 14));
        token.ThrowIfCancellationRequested();
        using var sensors = new HardwareSensors(snapshot.Notes, snapshot.Machine.IsAdministrator);
        NativeMetrics.CpuTimes? previous = NativeMetrics.ReadCpuTimes();
        detailedProgress?.Report(new ScanProgress("Sensores", "Observación de recursos preparada con los sensores accesibles.", 4, 14));
        for (var index = 0; index < 10; index++)
        {
            progress?.Report($"Observando recursos: muestra {index + 1} de 10…");
            if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1))) token.ThrowIfCancellationRequested();
            var current = NativeMetrics.ReadCpuTimes();
            var memory = NativeMetrics.ReadMemory();
            var sensor = sensors.Read();
            var sample = new MetricSample
            {
                CpuPercent = previous is { } before && current is { } now
                    ? AcquisitionNormalization.CpuPercent(before.Idle, before.Kernel, before.User, now.Idle, now.Kernel, now.User) : null,
                MemoryPercent = memory is { TotalBytes: > 0 } m ? AcquisitionNormalization.Percent((1 - m.AvailableBytes / (double)m.TotalBytes) * 100) : null,
                CpuTemperature = sensor.CpuTemperature,
                GpuTemperature = sensor.GpuTemperature,
                GpuPercent = sensor.GpuPercent
            };
            snapshot.Samples.Add(sample);
            detailedProgress?.Report(new ScanProgress("Recursos", $"Muestra {index + 1} de 10 recibida.", index + 5, 14, sample));
            previous = current;
        }
        if (snapshot.Samples.Any(s => s.CpuPercent is null)) snapshot.Notes.Add("CPU: Windows no entregó alguna lectura de uso; queda como desconocida.");
        if (snapshot.Samples.Any(s => s.MemoryPercent is null)) snapshot.Notes.Add("Memoria: Windows no entregó alguna lectura de uso; queda como desconocida.");
        if (snapshot.Samples.All(s => s.CpuTemperature is null)) snapshot.Notes.Add("Temperatura CPU no disponible. Algunos sensores requieren un controlador y permisos adicionales; el análisis básico funciona sin elevación.");
        if (snapshot.Samples.All(s => s.GpuTemperature is null)) snapshot.Notes.Add("Temperatura GPU no disponible en los sensores accesibles.");
        progress?.Report("Análisis de hardware finalizado.");
        return snapshot;
    }

    private static MachineInfo Machine(List<string> notes, CancellationToken token)
    {
        var system = Query("Sistema", "SELECT Manufacturer, Model, SystemFamily, SystemSKUNumber, TotalPhysicalMemory, PCSystemType FROM Win32_ComputerSystem", notes, token).FirstOrDefault();
        var product = Query("Producto", "SELECT Name, Version FROM Win32_ComputerSystemProduct", notes, token).FirstOrDefault();
        var enclosures = Query("Chassis", "SELECT ChassisTypes FROM Win32_SystemEnclosure", notes, token);
        var os = Query("Windows", "SELECT Caption, Version, LastBootUpTime FROM Win32_OperatingSystem", notes, token).FirstOrDefault();
        var cpus = Query("CPU", "SELECT Name, NumberOfLogicalProcessors FROM Win32_Processor", notes, token);
        var gpus = Query("GPU", "SELECT Name FROM Win32_VideoController", notes, token);
        var battery = NativeMetrics.ReadPowerStatus();
        var memory = NativeMetrics.ReadMemory();
        var classification = DeviceClassifier.Classify(Number(system, "PCSystemType") is { } pcSystemType ? (int)pcSystemType : null,
            enclosures.SelectMany(row => row.GetValueOrDefault("ChassisTypes") is ushort[] values ? values.Select(value => (int)value) : []), battery is { BatteryPresent: true });
        var power = (Guid: "", Name: "");
        TryProvider("Plan energético", notes, () => power = NativeMetrics.ReadPowerPlan());
        var isAdmin = false;
        TryProvider("Permisos", notes, () =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            isAdmin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        });
        double uptime = Environment.TickCount64 / 3_600_000d;
        if (Text(os, "LastBootUpTime") is { Length: > 0 } boot)
        {
            try { uptime = Math.Max(0, (DateTime.Now - ManagementDateTimeConverter.ToDateTime(boot)).TotalHours); }
            catch (ArgumentOutOfRangeException) { notes.Add("Tiempo de actividad: se usó el contador nativo porque WMI no entregó una fecha válida."); }
        }
        return new MachineInfo
        {
            Manufacturer = Text(system, "Manufacturer"), Model = Text(system, "Model"),
            ProductName = new[] { Text(product, "Version"), Text(system, "SystemFamily"), Text(product, "Name") }.FirstOrDefault(OemDriverService.IsUsableIdentity) ?? "",
            SystemSku = OemDriverService.IsUsableIdentity(Text(system, "SystemSKUNumber")) ? Text(system, "SystemSKUNumber") : "",
            DeviceKind = classification.Kind, DeviceTypeEvidence = classification.Evidence,
            OsName = Text(os, "Caption"), OsVersion = Text(os, "Version"),
            CpuName = string.Join(" / ", cpus.Select(c => Text(c, "Name")).Distinct()),
            GpuName = string.Join(" / ", gpus.Select(c => Text(c, "Name")).Distinct()),
            LogicalCores = cpus.Count > 0 ? (int)cpus.Sum(c => Number(c, "NumberOfLogicalProcessors") ?? 0) : Environment.ProcessorCount,
            TotalMemoryGb = (memory?.TotalBytes ?? (ulong)(Number(system, "TotalPhysicalMemory") ?? 0)) / 1073741824d,
            UptimeHours = uptime, PowerPlanGuid = power.Guid, PowerPlanName = power.Name,
            IsLaptop = classification.Kind == DeviceKind.Laptop,
            OnBattery = battery is { OnBattery: true }, IsAdministrator = isAdmin
        };
    }

    private static IEnumerable<StorageInfo> Drives(List<string> notes, CancellationToken token)
    {
        foreach (var drive in Query("Unidades", "SELECT DeviceID, VolumeName, FileSystem, DriveType, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 2 OR DriveType = 3", notes, token))
        {
            var size = Number(drive, "Size");
            var free = Number(drive, "FreeSpace");
            if (size is null || free is null)
            {
                notes.Add($"Unidad {Text(drive, "DeviceID")}: Windows no entregó capacidad o espacio libre; se omitió para evitar un valor inventado.");
                continue;
            }
            yield return new StorageInfo
            {
                Name = Text(drive, "DeviceID"), Label = Text(drive, "VolumeName"), FileSystem = Text(drive, "FileSystem"),
                Kind = Number(drive, "DriveType") == 2 ? "Extraíble" : "Local", TotalGb = size.Value / 1073741824d, FreeGb = free.Value / 1073741824d
            };
        }
    }

    private static List<DriverInfo> Drivers(List<string> notes, CancellationToken token)
    {
        var signed = new Dictionary<string, DriverInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Query("Drivers", "SELECT DeviceID, DeviceName, Manufacturer, DriverProviderName, DriverVersion, DriverDate, IsSigned, DeviceClass FROM Win32_PnPSignedDriver", notes, token))
        {
            var id = Text(row, "DeviceID");
            if (id.Length == 0) continue;
            signed[id] = new DriverInfo
            {
                DeviceName = Text(row, "DeviceName"), Manufacturer = Text(row, "Manufacturer"), Provider = Text(row, "DriverProviderName"),
                Version = Text(row, "DriverVersion"), Date = AcquisitionNormalization.DriverDate(Text(row, "DriverDate")),
                Signed = row.GetValueOrDefault("IsSigned") as bool?, DeviceClass = Text(row, "DeviceClass")
            };
        }
        var devices = Query("Dispositivos PnP", "SELECT DeviceID, Name, Manufacturer, PNPClass, HardwareID, ConfigManagerErrorCode FROM Win32_PnPEntity", notes, token)
            .Select(row => new DeviceInventory(Text(row, "DeviceID"), Text(row, "Name"), Text(row, "Manufacturer"), Text(row, "PNPClass"),
                SafeHardwareId(row.GetValueOrDefault("HardwareID") as string[]), Number(row, "ConfigManagerErrorCode") is { } code ? (int)code : null));
        return AcquisitionNormalization.MergeDrivers(signed, devices).OrderBy(d => d.DeviceName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static string SafeHardwareId(string[]? ids) => ids?.FirstOrDefault(id =>
        (id.StartsWith("PCI\\VEN_", StringComparison.OrdinalIgnoreCase) || id.StartsWith("USB\\VID_", StringComparison.OrdinalIgnoreCase) ||
         id.StartsWith("HID\\VID_", StringComparison.OrdinalIgnoreCase)) && id.Count(c => c == '\\') == 1) ?? "";

    private static IEnumerable<InventoryStartupEntry> Startup(List<string> notes, CancellationToken token)
    {
        var entries = new List<InventoryStartupEntry>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in Environment.Is64BitOperatingSystem ? new[] { RegistryView.Registry64, RegistryView.Registry32 } : new[] { RegistryView.Registry32 })
        foreach (var leaf in new[] { "Run", "RunOnce" })
        {
            token.ThrowIfCancellationRequested();
            TryProvider("Inicio (registro)", notes, () =>
            {
                using var registry = RegistryKey.OpenBaseKey(hive, view);
                using var key = registry.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\" + leaf, false);
                if (key is null) return;
                foreach (var name in key.GetValueNames())
                {
                    if (key.GetValue(name, "", RegistryValueOptions.DoNotExpandEnvironmentNames) is string command && command.Length > 0)
                        entries.Add(new InventoryStartupEntry(AcquisitionNormalization.PrivateCommand(name), AcquisitionNormalization.PrivateCommand(command), $"{hive} / {leaf} / {view}"));
                }
            });
        }
        foreach (var row in Query("Inicio (WMI)", "SELECT Name, Command FROM Win32_StartupCommand", notes, token))
            entries.Add(new InventoryStartupEntry(AcquisitionNormalization.PrivateCommand(Text(row, "Name")), AcquisitionNormalization.PrivateCommand(Text(row, "Command")), "Inicio de Windows (WMI)"));
        notes.Add("Inicio: se revisaron registro y WMI. Windows puede mostrar otras aplicaciones o tareas de inicio en Configuración.");
        return entries.DistinctBy(e => e.Name + "\n" + e.Command, StringComparer.OrdinalIgnoreCase).OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    private static List<Dictionary<string, object?>> Query(string provider, string query, List<string> notes, CancellationToken token)
    {
        var rows = new List<Dictionary<string, object?>>();
        TryProvider(provider, notes, () =>
        {
            token.ThrowIfCancellationRequested();
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\cimv2"), new ObjectQuery(query),
                new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(5), ReturnImmediately = true });
            using var results = searcher.Get();
            foreach (ManagementObject row in results)
            {
                using (row)
                {
                    token.ThrowIfCancellationRequested();
                    var values = new Dictionary<string, object?>();
                    foreach (PropertyData property in row.Properties) values[property.Name] = property.Value;
                    rows.Add(values);
                }
            }
            if (rows.Count == 0) notes.Add($"{provider}: la fuente no entregó registros.");
        });
        return rows;
    }

    private static void TryProvider(string provider, List<string> notes, Action action)
    {
        try { action(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Security.SecurityException or
            System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception or InvalidOperationException or IOException or NotSupportedException)
        {
            notes.Add($"{provider}: no se pudo leer esta fuente ({ex.GetType().Name}, 0x{ex.HResult:X8}). El resto del análisis continúa.");
        }
    }

    private static string Text(IReadOnlyDictionary<string, object?>? values, string name) =>
        values?.GetValueOrDefault(name)?.ToString()?.Trim() ?? "";

    private static double? Number(IReadOnlyDictionary<string, object?>? values, string name) =>
        double.TryParse(Text(values, name), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;
}
