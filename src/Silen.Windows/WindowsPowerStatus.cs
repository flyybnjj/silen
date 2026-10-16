using System.Runtime.InteropServices;

namespace Silen.Windows;

internal static class WindowsPowerStatus
{
    internal static bool? ReadAc() => GetSystemPowerStatus(out var state)
        ? state.Ac switch { 1 => true, 0 => false, _ => null } : null;
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus { public byte Ac, Flags, Life, Saver; public uint Seconds, FullSeconds; }
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out PowerStatus state);
}
