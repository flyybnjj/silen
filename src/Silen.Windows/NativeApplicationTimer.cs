using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Silen.Windows;

internal sealed class NativeApplicationTimerPlatform : IApplicationTimerPlatform
{
    public TimerResolutionState Read()
    {
        Check(NtQueryTimerResolution(out var coarsest, out var finest, out var current));
        return new(coarsest, finest, current);
    }
    public void Request(uint interval) => Check(NtSetTimerResolution(interval, true, out _));
    public void Release() => Check(NtSetTimerResolution(0, false, out _));
    private static void Check(int status) { if (status < 0) throw new InvalidOperationException($"Windows devolvió NTSTATUS 0x{status:X8}."); }
    [DllImport("ntdll.dll")] private static extern int NtQueryTimerResolution(out uint coarsest, out uint finest, out uint current);
    [DllImport("ntdll.dll")] private static extern int NtSetTimerResolution(uint interval, [MarshalAs(UnmanagedType.U1)] bool request, out uint current);
}

internal sealed class ApplicationProcessProbe : IApplicationProcessProbe
{
    public bool? IsRunning(string path)
    {
        var unknown = false;
        try
        {
            var candidates = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path));
            var found = false;
            foreach (var candidate in candidates)
            {
                using (candidate)
                {
                    try
                    {
                        using var handle = OpenProcess(0x1000, false, candidate.Id);
                        if (handle.IsInvalid) { unknown = true; continue; }
                        var name = new StringBuilder(32768); var length = (uint)name.Capacity;
                        if (!QueryFullProcessImageName(handle, 0, name, ref length)) { unknown = true; continue; }
                        if (Path.GetFullPath(name.ToString()).Equals(path, StringComparison.OrdinalIgnoreCase)) found = true;
                    }
                    catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException) { unknown = true; }
                }
            }
            return found ? true : unknown ? null : false;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException) { return null; }
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref uint size);
}
