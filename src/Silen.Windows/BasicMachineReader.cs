using System.Security;
using Microsoft.Win32;
using Silen.Core;

namespace Silen.Windows;

// Vista previa local, sin WMI, red, serie ni datos de una sesión anterior.
// No es un diagnóstico completo ni habilita acciones del sistema.
internal static class BasicMachineReader
{
    private const string Bios = @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\BIOS";
    private const string Cpu = @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0";

    internal static MachineInfo Read() => Read((key, name) => Registry.GetValue(key, name, null) as string,
        () => NativeMetrics.ReadMemory()?.TotalBytes);

    internal static MachineInfo Read(Func<string, string, string?> read, Func<ulong?> memory)
    {
        string Text(string key, string name)
        {
            try
            {
                var value = read(key, name)?.Trim();
                return value is { Length: > 0 and <= 256 } && OemDriverService.IsUsableIdentity(value) ? value : "";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException) { return ""; }
        }
        return new()
        {
            Manufacturer = Text(Bios, "SystemManufacturer"), Model = Text(Bios, "SystemProductName"),
            CpuName = Text(Cpu, "ProcessorNameString"), TotalMemoryGb = (memory() ?? 0) / 1073741824d,
            OsName = "Windows", OsVersion = Environment.OSVersion.Version.ToString(),
            LogicalCores = Environment.ProcessorCount
        };
    }
}
