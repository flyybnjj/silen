namespace Silen.Core;

/// <summary>Lista positiva de datos exportables: no serializar SystemSnapshot a un proveedor.</summary>
public sealed record BloomDiagnosticSummary(int LogicalCores, double MemoryGb, bool OnBattery,
    int Samples, double? CpuPercent, double? MemoryPercent, double? GpuPercent,
    double? CpuTemperature, double? GpuTemperature, int Drives, int LowSpaceDrives,
    int DriverProblems, int UnsignedDrivers, int StartupEntries, int Processes, int NetworkAdapters,
    bool? UpdatePaused, bool? UpdateServiceEnabled, bool? UpdateServiceRunning)
{
    public static BloomDiagnosticSummary From(SystemSnapshot snapshot)
    {
        static double? Mean(IEnumerable<double?> source)
        {
            var values = source.Where(value => value.HasValue && double.IsFinite(value.Value)).Select(value => value!.Value).ToArray();
            return values.Length == 0 ? null : Math.Round(values.Average(), 1);
        }
        return new(snapshot.Machine.LogicalCores, double.IsFinite(snapshot.Machine.TotalMemoryGb) ? Math.Round(snapshot.Machine.TotalMemoryGb, 1) : 0,
            snapshot.Machine.OnBattery, snapshot.Samples.Count, Mean(snapshot.Samples.Select(sample => sample.CpuPercent)),
            Mean(snapshot.Samples.Select(sample => sample.MemoryPercent)), Mean(snapshot.Samples.Select(sample => sample.GpuPercent)),
            Mean(snapshot.Samples.Select(sample => sample.CpuTemperature)), Mean(snapshot.Samples.Select(sample => sample.GpuTemperature)),
            snapshot.Drives.Count, snapshot.Drives.Count(drive => drive.FreePercent < 15),
            snapshot.Drivers.Count(driver => driver.ProblemCode is > 0), snapshot.Drivers.Count(driver => driver.Signed == false),
            snapshot.Startup.Count, snapshot.Processes.Count, snapshot.Networks.Count,
            snapshot.WindowsUpdate?.Paused, snapshot.WindowsUpdate?.ServiceEnabled, snapshot.WindowsUpdate?.ServiceRunning);
    }
}
