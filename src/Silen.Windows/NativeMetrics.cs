using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Silen.Windows;

internal static class NativeMetrics
{
    internal readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);
    internal readonly record struct Memory(ulong TotalBytes, ulong AvailableBytes);
    internal readonly record struct PowerStatus(bool BatteryPresent, bool OnBattery);

    internal static CpuTimes? ReadCpuTimes() => GetSystemTimes(out var idle, out var kernel, out var user) ? new CpuTimes(idle, kernel, user) : null;

    internal static Memory? ReadMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? new Memory(status.TotalPhysical, status.AvailablePhysical) : null;
    }

    internal static PowerStatus? ReadPowerStatus() => GetSystemPowerStatus(out var status)
        ? new PowerStatus(status.BatteryFlag != 128 && status.BatteryFlag != 255, status.AcLineStatus == 0 && status.BatteryFlag != 128) : null;

    internal static (string Guid, string Name) ReadPowerPlan()
    {
        var error = PowerGetActiveScheme(IntPtr.Zero, out var pointer);
        if (error != 0) throw new Win32Exception((int)error);
        try
        {
            var guid = Marshal.PtrToStructure<Guid>(pointer);
            uint bytes = 0;
            error = PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, null, ref bytes);
            if (error != 0 && error != 234) throw new Win32Exception((int)error);
            if (bytes == 0 || bytes > 65536) return (guid.ToString(), "");
            var buffer = new byte[bytes];
            error = PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, buffer, ref bytes);
            if (error != 0) throw new Win32Exception((int)error);
            return (guid.ToString(), Encoding.Unicode.GetString(buffer).TrimEnd('\0'));
        }
        finally { LocalFree(pointer); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public uint BatteryLifeTime, BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);
    [DllImport("powrprof.dll")]
    private static extern uint PowerReadFriendlyName(IntPtr root, ref Guid scheme, IntPtr subGroup, IntPtr setting, byte[]? buffer, ref uint size);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
