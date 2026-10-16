using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace Silen.Windows;

internal sealed class HardwareSensors : IDisposable
{
    private Computer? _computer;
    private ManagementObjectSearcher? _cpuProvider;
    private readonly List<string> _notes;
    private readonly HashSet<string> _failed = [];
    internal readonly record struct Readings(double? CpuTemperature, double? GpuTemperature, double? GpuPercent);



    internal static Computer CreateReadOnlyComputer() => new() { IsCpuEnabled = false, IsGpuEnabled = true };

    internal HardwareSensors(List<string> notes, bool isAdministrator)
    {
        _notes = notes;
        notes.Add("Sensores: no se habilitan proveedores de kernel ni se instala un controlador. La temperatura CPU requiere un proveedor WMI de sensores que ya esté funcionando.");
        if (!isAdministrator) notes.Add("Sensores: se usan los permisos actuales; la aplicación no solicita elevación.");
        FindExistingCpuProvider();
        try
        {
            _computer = CreateReadOnlyComputer();
            _computer.Open();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            notes.Add($"Sensores no disponibles ({ex.GetType().Name}). CPU y memoria se observan con las API de Windows.");
            CloseComputer();
        }
    }

    internal Readings Read()
    {
        var cpu = new List<double>();
        var gpu = new List<double>();
        var gpuLoads = new List<double>();
        ReadExistingCpuProvider(cpu);
        if (_computer is not null)
            foreach (var hardware in _computer.Hardware) ReadHardware(hardware, hardware.HardwareType, cpu, gpu, gpuLoads);
        return new Readings(cpu.Count > 0 ? cpu.Max() : null, gpu.Count > 0 ? gpu.Max() : null, gpuLoads.Count > 0 ? gpuLoads.Max() : null);
    }

    private void FindExistingCpuProvider()
    {
        foreach (var name in new[] { "LibreHardwareMonitor", "OpenHardwareMonitor" })
        {
            var candidate = new ManagementObjectSearcher(new ManagementScope($@"\\.\root\{name}"),
                new ObjectQuery("SELECT Identifier, SensorType, Value FROM Sensor WHERE SensorType = 'Temperature'"),
                new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(2) });
            try
            {
                using var rows = candidate.Get();
                _cpuProvider = candidate;
                _notes.Add($"Sensores CPU: se usa el proveedor WMI existente {name}.");
                return;
            }
            catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                candidate.Dispose();
            }
        }
    }

    private void ReadExistingCpuProvider(List<double> cpu)
    {
        if (_cpuProvider is null) return;
        try
        {
            using var rows = _cpuProvider.Get();
            foreach (ManagementObject row in rows)
            {
                using (row)
                {
                    var value = double.TryParse(Convert.ToString(row["Value"], CultureInfo.InvariantCulture), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var parsed) ? (double?)parsed : null;
                    if (AcquisitionNormalization.CpuTemperature(row["Identifier"] as string, row["SensorType"] as string, value) is { } temperature) cpu.Add(temperature);
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _notes.Add($"El proveedor WMI de temperatura dejó de responder ({ex.GetType().Name}); las lecturas quedan como desconocidas.");
            _cpuProvider.Dispose();
            _cpuProvider = null;
        }
    }

    private void ReadHardware(IHardware hardware, HardwareType parentType, List<double> cpu, List<double> gpu, List<double> gpuLoads)
    {
        try
        {
            hardware.Update();
            bool isCpu = parentType == HardwareType.Cpu;
            bool isGpu = parentType is HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia;
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.Value is not { } value || !float.IsFinite(value)) continue;
                if (sensor.SensorType == SensorType.Temperature && value is > -50 and < 150)
                {
                    if (isCpu) cpu.Add(value);
                    if (isGpu) gpu.Add(value);
                }

                if (isGpu && sensor.SensorType == SensorType.Load && (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || sensor.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase)) &&
                    AcquisitionNormalization.Percent(value) is { } load) gpuLoads.Add(load);
            }
            foreach (var sub in hardware.SubHardware) ReadHardware(sub, parentType, cpu, gpu, gpuLoads);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (_failed.Add(hardware.Identifier.ToString())) _notes.Add($"Un sensor no se pudo leer ({ex.GetType().Name}); su medición queda como desconocida.");
        }
    }

    private void CloseComputer()
    {
        try { _computer?.Close(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _notes.Add($"No se pudo cerrar por completo el proveedor de sensores ({ex.GetType().Name})."); }
        finally { _computer = null; }
    }

    public void Dispose()
    {
        _cpuProvider?.Dispose();
        _cpuProvider = null;
        CloseComputer();
    }
}
