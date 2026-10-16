using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Silen.Core;

namespace Silen.Windows;

public static class MemoryDiagnostics
{
    public static MemoryObservation? ReadFast(bool? automaticPagefile = null)
    {
        var info = new PerformanceInformation { Size = (uint)Marshal.SizeOf<PerformanceInformation>() };
        if (!K32GetPerformanceInfo(ref info, info.Size)) return null;
        try
        {
            var page = checked((long)info.PageSize.ToUInt64());
            return new(checked((long)info.PhysicalTotal.ToUInt64() * page), checked((long)info.PhysicalAvailable.ToUInt64() * page),
                checked((long)info.CommitTotal.ToUInt64() * page), checked((long)info.CommitLimit.ToUInt64() * page), automaticPagefile, DateTimeOffset.Now);
        }
        catch (OverflowException) { return null; }
    }
    public static Task<bool?> ReadAutomaticPagefileAsync(CancellationToken token = default) => Task.Run<bool?>(() =>
    {
        token.ThrowIfCancellationRequested();
        try
        {
            using var search = new ManagementObjectSearcher(new ManagementScope("root\\cimv2"), new ObjectQuery("SELECT AutomaticManagedPagefile FROM Win32_ComputerSystem"), new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(3) });
            using var rows = search.Get();
            foreach (ManagementObject row in rows) using (row) { token.ThrowIfCancellationRequested(); return row["AutomaticManagedPagefile"] as bool?; }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { }
        return null;
    }, token);
    public static Task<IReadOnlyList<ProcessInfo>> ReadProcessesAsync(CancellationToken token = default) => Task.Run<IReadOnlyList<ProcessInfo>>(() =>
    {
        var result = new List<ProcessInfo>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                token.ThrowIfCancellationRequested();
                try { result.Add(new(process.ProcessName, process.Id, process.WorkingSet64 / 1048576d)); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
        }
        return result.OrderByDescending(process => process.MemoryMb).Take(15).ToArray();
    }, token);
    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint Size;
        public UIntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable, SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool K32GetPerformanceInfo(ref PerformanceInformation information, uint size);
}
