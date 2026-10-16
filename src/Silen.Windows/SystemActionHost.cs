using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Silen.Windows;

/// <summary>Canal local de una sola operación, ligado al proceso, usuario, sesión y resumen aprobado en UAC.</summary>
public static class SystemActionHost
{
    public const string Argument = "--system-action-host";
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[0] != Argument || !Guid.TryParseExact(args[1], "N", out _) ||
            !int.TryParse(args[2], out var parentId) || parentId <= 0 || args[3].Length != 64 || !args[3].All(Uri.IsHexDigit)) return 2;
        using var timeout = new CancellationTokenSource(SystemActionPolicy.SessionTimeout);
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return 3;
            using var parent = Process.GetProcessById(parentId);
            DemandPeer(parent, requireElevated: false);
            using var pipe = new NamedPipeClientStream(".", "Silen.System." + args[1], PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(timeout.Token);
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var serverId) || serverId != parentId) return 4;
            var bytes = await ReadMessageAsync(pipe, SystemActionPolicy.MaximumRequestBytes, timeout.Token);
            var digest = Convert.ToHexString(SHA256.HashData(bytes));
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(digest), Encoding.ASCII.GetBytes(args[3].ToUpperInvariant()))) return 5;
            var request = SystemActionPolicy.Parse(bytes);
            var response = await ExecuteWhileConnectedAsync(pipe, async operationToken =>
            {
                if (request.Action == SystemActionKind.CreateRestorePoint)
                {
                    var point = await new NativeSystemRestoreService().PrepareAsync(operationToken);
                    return new SystemActionResponse(new(point.CanContinueAutomatically, point.Message), RestorePoint: point);
                }
                if (request.Action == SystemActionKind.DownloadDrivers)
                {
                    var offers = request.Updates!.Select(update => new Silen.Core.DriverUpdateInfo("", "", "", "", "", update.UpdateId.ToString(), update.Revision)).ToArray();
                    var download = await new DriverDownloadService().DownloadAsync(offers, operationToken);
                    return new SystemActionResponse(new(download.Success, download.Message), Download: download);
                }
                operationToken.ThrowIfCancellationRequested();
                var native = NativeOptimizationService.CreateSystemExecutor();
                var outcome = request.Action switch
                {
                    SystemActionKind.SetToggle => await native.SetToggleAsync(request.TargetId!, request.Enabled!.Value, operationToken),
                    SystemActionKind.ResumeWindowsUpdate => await native.ResumeWindowsUpdateAsync(operationToken),
                    SystemActionKind.Undo => await native.UndoAsync(request.TargetId!, operationToken),
                    SystemActionKind.InspectHistory => new OptimizationOutcome(true, "Respaldos del sistema consultados con autorización."),
                    _ => throw new ArgumentException("Operación no permitida.")
                };
                var overview = await native.InspectAsync(operationToken);
                overview = overview with { Toggles = overview.Toggles.Where(t => SystemActionPolicy.IsPrivilegedToggle(t.Id)).ToList(), StartupEntries = [], PowerSchemes = [] };
                return new SystemActionResponse(outcome, overview);
            }, timeout.Token);
            await WriteMessageAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(response, SystemActionPolicy.Json), SystemActionPolicy.MaximumResponseBytes, timeout.Token);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or Win32Exception or InvalidOperationException or OperationCanceledException or System.Security.SecurityException) { return 6; }
    }

    internal static async Task<T> ExecuteWhileConnectedAsync<T>(Stream connection, Func<CancellationToken, Task<T>> action, CancellationToken token)
    {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var monitor = CancellationTokenSource.CreateLinkedTokenSource(token);
        var disconnected = MonitorConnectionAsync(connection, operation, monitor.Token);
        try { return await action(operation.Token); }
        finally { monitor.Cancel(); await disconnected; }
    }

    private static async Task MonitorConnectionAsync(Stream connection, CancellationTokenSource operation, CancellationToken token)
    {
        try
        {
            // El protocolo admite una sola solicitud. EOF o cualquier dato posterior cierra la autorización.
            await connection.ReadExactlyAsync(new byte[1], token);
            operation.Cancel();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { operation.Cancel(); }
    }

    public static NamedPipeServerStream CreateSessionPipe(string sessionId)
    {
        if (!Guid.TryParseExact(sessionId, "N", out _)) throw new ArgumentException("Sesión inválida.");
        return new NamedPipeServerStream("Silen.System." + sessionId, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 4096, 4096);
    }

    public static async Task<SystemActionResponse> ExchangeAsync(NamedPipeServerStream pipe, Process helper, byte[] request, CancellationToken token)
    {
        await pipe.WaitForConnectionAsync(token);
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var clientId) || clientId != helper.Id)
            throw new UnauthorizedAccessException("El ejecutor conectado no corresponde al proceso autorizado.");
        var machine = new StringBuilder(256);
        if (!GetNamedPipeClientComputerName(pipe.SafePipeHandle, machine, (uint)machine.Capacity) ||
            !string.Equals(machine.ToString().TrimStart('\\'), Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("El ejecutor requiere una conexión local.");
        DemandPeer(helper, requireElevated: true);
        await WriteMessageAsync(pipe, request, SystemActionPolicy.MaximumRequestBytes, token);
        var response = await ReadMessageAsync(pipe, SystemActionPolicy.MaximumResponseBytes, token);
        return JsonSerializer.Deserialize<SystemActionResponse>(response, SystemActionPolicy.Json)
            ?? throw new InvalidDataException("El ejecutor no devolvió un resultado válido.");
    }

    private static void DemandPeer(Process peer, bool requireElevated)
    {
        using var current = Process.GetCurrentProcess();
        using var identity = WindowsIdentity.GetCurrent();
        if (!OpenProcessToken(peer.SafeHandle, 8, out var token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        using (token)
        using (var peerIdentity = new WindowsIdentity(token.DangerousGetHandle()))
        {
            SystemActionPolicy.ValidatePeer(identity.User!.Value, peerIdentity.User!.Value, current.SessionId, peer.SessionId, false);
            if (new WindowsPrincipal(peerIdentity).IsInRole(WindowsBuiltInRole.Administrator) != requireElevated)
                throw new UnauthorizedAccessException("El proceso no tiene el nivel de privilegios esperado.");
        }
        var expected = Environment.ProcessPath ?? throw new InvalidOperationException("No se pudo identificar Silen.");
        var image = new StringBuilder(32768);
        var imageLength = image.Capacity;
        if (!QueryFullProcessImageName(peer.SafeHandle, 0, image, ref imageLength)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var path = image.ToString();
        if (!string.Equals(expected, path, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("La conexión no pertenece al ejecutable de Silen.");
    }

    private static async Task<byte[]> ReadMessageAsync(Stream stream, int maximum, CancellationToken token)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, token);
        var size = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (size <= 0 || size > maximum) throw new InvalidDataException("Mensaje fuera del límite permitido.");
        var bytes = new byte[size];
        await stream.ReadExactlyAsync(bytes, token);
        return bytes;
    }
    private static async Task WriteMessageAsync(Stream stream, byte[] bytes, int maximum, CancellationToken token)
    {
        if (bytes.Length is 0 || bytes.Length > maximum) throw new InvalidDataException("Mensaje fuera del límite permitido.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(bytes, token);
        await stream.FlushAsync(token);
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out int processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetNamedPipeClientComputerName(SafePipeHandle pipe, StringBuilder name, uint length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder image, ref int length);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
}
