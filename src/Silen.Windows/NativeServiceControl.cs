using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Silen.Windows;

internal sealed partial class NativeOptimizationPlatform
{
    public ServiceSnapshot? ReadService(string name)
    {
        OptionalServices.ValidateName(name);
        return ReadServiceCore(name);
    }
    public ServiceSnapshot? ReadUpdateService() => ReadServiceCore("wuauserv");
    public bool CanManageUpdateService() => CanManageServiceCore("wuauserv");
    public void SetUpdateServiceStartType(int startType) => SetServiceStartTypeCore("wuauserv", startType);
    public void SetUpdateServiceRunning(bool running) => SetServiceRunningCore("wuauserv", running);

    private static ServiceSnapshot? ReadServiceCore(string name)
    {
        using var manager = OpenManager();
        using var service = OpenServiceW(manager, name, 0x0001 | 0x0004);
        if (service.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1060) return null;
            throw new Win32Exception(error);
        }
        QueryServiceConfigW(service, IntPtr.Zero, 0, out var required);
        if (Marshal.GetLastWin32Error() != 122 || required is 0 or > 65536) throw new Win32Exception(Marshal.GetLastWin32Error());
        var buffer = Marshal.AllocHGlobal(checked((int)required));
        try
        {
            if (!QueryServiceConfigW(service, buffer, required, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var configuration = Marshal.PtrToStructure<ServiceConfiguration>(buffer);
            if (configuration.StartType is < 2 or > 4) throw new InvalidOperationException("El servicio usa un inicio no admitido; se conserva su configuración.");
            var status = ReadStatus(service);
            if (status.CurrentState is not (1 or 4)) throw new InvalidOperationException("El servicio está cambiando de estado. Espera y actualiza los ajustes.");
            return new(checked((int)configuration.StartType), status.CurrentState == 4);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    public bool CanManageService(string name)
    {
        OptionalServices.ValidateName(name);
        return CanManageServiceCore(name);
    }
    private static bool CanManageServiceCore(string name)
    {
        try
        {
            using var manager = OpenManager();
            using var service = OpenServiceW(manager, name, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
            return !service.IsInvalid;
        }
        catch (Win32Exception) { return false; }
    }

    public void SetServiceStartType(string name, int startType)
    {
        OptionalServices.ValidateName(name);
        SetServiceStartTypeCore(name, startType);
    }
    private static void SetServiceStartTypeCore(string name, int startType)
    {
        if (startType is < 2 or > 4) throw new ArgumentException("Tipo de inicio no permitido.");
        using var manager = OpenManager();
        using var service = OpenRequiredService(manager, name, 0x0002);
        // Conserva cuenta, ejecutable, dependencias, recuperación e inicio demorado.
        if (!ChangeServiceConfigW(service, uint.MaxValue, (uint)startType, uint.MaxValue,
            null, null, IntPtr.Zero, null, null, null, null)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void SetServiceRunning(string name, bool running)
    {
        OptionalServices.ValidateName(name);
        SetServiceRunningCore(name, running);
    }
    private static void SetServiceRunningCore(string name, bool running)
    {
        using var manager = OpenManager();
        using var service = OpenRequiredService(manager, name, 0x0004 | (running ? 0x0010u : 0x0020u));
        var target = running ? 4u : 1u;
        var status = ReadStatus(service);
        if (status.CurrentState == target) return;
        if (status.CurrentState is not (1 or 4)) throw new InvalidOperationException("El servicio está cambiando de estado. No se forzó ninguna transición.");
        if (running)
        {
            if (!StartServiceW(service, 0, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        else if (!ControlService(service, 1, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var timer = Stopwatch.StartNew();
        while (ReadStatus(service).CurrentState != target)
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(8)) throw new IOException("El servicio no confirmó el estado solicitado. El respaldo permite revisar y restaurar el cambio.");
            Thread.Sleep(100);
        }
    }

    private static ServiceHandle OpenManager()
    {
        var handle = OpenSCManagerW(null, null, 1);
        if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        return handle;
    }
    private static ServiceHandle OpenRequiredService(ServiceHandle manager, string name, uint access)
    {
        var handle = OpenServiceW(manager, name, access);
        if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        return handle;
    }
    private static ServiceStatus ReadStatus(ServiceHandle service) => QueryServiceStatus(service, out var status)
        ? status : throw new Win32Exception(Marshal.GetLastWin32Error());

    private sealed class ServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public ServiceHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceConfiguration
    {
        public uint Type, StartType, ErrorControl;
        public IntPtr Binary, LoadGroup;
        public uint Tag;
        public IntPtr Dependencies, Account, DisplayName;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus { public uint Type, CurrentState, Controls, ExitCode, SpecificExitCode, CheckPoint, WaitHint; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ServiceHandle OpenSCManagerW(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ServiceHandle OpenServiceW(ServiceHandle manager, string name, uint access);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceConfigW(ServiceHandle service, IntPtr config, uint size, out uint required);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatus(ServiceHandle service, out ServiceStatus status);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfigW(ServiceHandle service, uint type, uint start, uint error,
        string? binary, string? group, IntPtr tag, string? dependencies, string? account, string? password, string? display);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ControlService(ServiceHandle service, uint control, out ServiceStatus status);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartServiceW(ServiceHandle service, uint count, IntPtr arguments);
}
