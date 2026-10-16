using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using System.Security;

namespace Silen.Windows;

internal sealed record RestorePointIdentity(long SequenceNumber, DateTimeOffset CreatedAt, string Description);
internal sealed record RestoreNativeResult(bool Success, int Error, long SequenceNumber);
internal interface ISystemRestoreApi
{
    IDisposable Initialize();
    IReadOnlyList<RestorePointIdentity> ReadPoints();
    RestoreNativeResult Begin(string description);
    RestoreNativeResult End(long sequence);
}

public sealed class NativeSystemRestoreService : IRestorePointService
{
    private readonly ISystemRestoreApi api;
    internal NativeSystemRestoreService() : this(new WindowsSystemRestoreApi()) { }
    internal NativeSystemRestoreService(ISystemRestoreApi api) => this.api = api;
    public Task<RestorePointResult> PrepareAsync(CancellationToken cancellationToken = default) => Task.Run(() => Prepare(cancellationToken));

    private RestorePointResult Prepare(CancellationToken token)
    {
        var began = false;
        try
        {
            token.ThrowIfCancellationRequested();
            using var com = api.Initialize();
            var before = api.ReadPoints().Select(point => point.SequenceNumber).ToHashSet();
            token.ThrowIfCancellationRequested();
            var description = "Silen · antes de aplicar · " + Guid.NewGuid().ToString("N");
            var start = api.Begin(description);
            if (!start.Success || start.Error != 0)
                return new(start.Error == 1058 ? RestorePointState.Disabled : RestorePointState.Failed, ErrorCode: start.Error);
            if (start.SequenceNumber <= 0) return new(RestorePointState.Failed);
            began = true;
            // La API no admite cancelación. Cerrar siempre el cambio nativo ya iniciado;
            // no usar CANCELLED_OPERATION ni borrar el punto al cancelar la interfaz.
            var end = api.End(start.SequenceNumber);
            token.ThrowIfCancellationRequested();
            if (!end.Success || end.Error != 0) return new(RestorePointState.Failed, ErrorCode: end.Error);
            var verified = api.ReadPoints().SingleOrDefault(point => point.SequenceNumber == start.SequenceNumber);
            token.ThrowIfCancellationRequested();
            if (verified is null) return new(RestorePointState.Failed);
            var created = !before.Contains(verified.SequenceNumber) && verified.Description == description;
            return new(created ? RestorePointState.Created : RestorePointState.Reused, verified.SequenceNumber, verified.CreatedAt);
        }
        catch (OperationCanceledException) { return new(RestorePointState.Cancelled); }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or SecurityException or
            InvalidOperationException or ArgumentException or FormatException or OverflowException or DllNotFoundException or EntryPointNotFoundException or Win32Exception or COMException)
        { return new(began ? RestorePointState.Failed : RestorePointState.Unavailable, ErrorCode: ex is Win32Exception win32 ? win32.NativeErrorCode : null); }
    }

    private sealed class WindowsSystemRestoreApi : ISystemRestoreApi
    {
        public IDisposable Initialize() => SystemRestoreComSecurity.Initialize();
        public IReadOnlyList<RestorePointIdentity> ReadPoints()
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\default"),
                new ObjectQuery("SELECT SequenceNumber, CreationTime, Description FROM SystemRestore"),
                new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(20), ReturnImmediately = true });
            using var objects = searcher.Get();
            var points = new List<RestorePointIdentity>();
            foreach (ManagementObject item in objects)
            {
                using (item)
                {
                    if (points.Count >= 4096) throw new InvalidOperationException("El catálogo excede el límite de verificación.");
                    var date = ManagementDateTimeConverter.ToDateTime(Convert.ToString(item["CreationTime"], System.Globalization.CultureInfo.InvariantCulture)!);
                    points.Add(new(Convert.ToInt64(item["SequenceNumber"], System.Globalization.CultureInfo.InvariantCulture),
                        new DateTimeOffset(date), Convert.ToString(item["Description"], System.Globalization.CultureInfo.InvariantCulture) ?? ""));
                }
            }
            return points;
        }
        public RestoreNativeResult Begin(string description) => Call(100, 0, description);
        public RestoreNativeResult End(long sequence) => Call(101, sequence, "");
        private static RestoreNativeResult Call(uint eventType, long sequence, string description)
        {
            var info = new RestorePointInfo { EventType = eventType, RestorePointType = 12, SequenceNumber = sequence, Description = description };
            var success = SRSetRestorePointW(ref info, out var status);
            return new(success, (int)status.Status, status.SequenceNumber);
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RestorePointInfo
        {
            public uint EventType;
            public uint RestorePointType;
            public long SequenceNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct StateManagerStatus { public uint Status; public long SequenceNumber; }
        [DllImport("srclient.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SRSetRestorePointW(ref RestorePointInfo info, out StateManagerStatus status);
    }
}

