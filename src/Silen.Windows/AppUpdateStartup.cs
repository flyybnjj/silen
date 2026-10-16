using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Silen.Windows;

public sealed record AppUpdateStartupTicket(string Session, int UpdaterId);

/// <summary>Acuse de arranque local, de un solo uso, ligado al proceso iniciado.</summary>
public static class AppUpdateStartup
{
    public const string Argument = "--update-startup";
    internal static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);

    public static AppUpdateStartupTicket Parse(string[] args)
    {
        if (args is not [Argument, var session, var updater] || !Guid.TryParseExact(session, "N", out _) ||
            !int.TryParse(updater, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pid) || pid <= 0)
            throw new ArgumentException("Acuse de arranque incompatible.");
        return new(session, pid);
    }

    internal static NamedPipeServerStream CreatePipe(string session)
    {
        if (!Guid.TryParseExact(session, "N", out _)) throw new ArgumentException("Sesión de arranque incompatible.");
        return new("Silen.Update.Startup." + session, PipeDirection.In, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 128, 128);
    }

    public static async Task ReportReadyAsync(AppUpdateStartupTicket ticket)
    {
        _ = Parse([Argument, ticket.Session, ticket.UpdaterId.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var pipe = new NamedPipeClientStream(".", "Silen.Update.Startup." + ticket.Session, PipeDirection.Out,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var server) || server != ticket.UpdaterId)
            throw new UnauthorizedAccessException("El acuse no pertenece al actualizador que inició Silen.");
        using var updater = Process.GetProcessById(ticket.UpdaterId);
        using var current = Process.GetCurrentProcess();
        if (updater.SessionId != current.SessionId)
            throw new UnauthorizedAccessException("El actualizador pertenece a otra sesión.");
        await pipe.WriteAsync(Guid.ParseExact(ticket.Session, "N").ToByteArray(), timeout.Token).ConfigureAwait(false);
        await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);
    }

    internal static async Task ConfirmAsync(NamedPipeServerStream pipe, string session, int expectedProcess,
        Task exited, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        var ready = ReadReadyAsync(pipe, session, expectedProcess, deadline.Token);
        try
        {
            if (await Task.WhenAny(ready, exited).ConfigureAwait(false) == exited)
            {
                await exited.ConfigureAwait(false);
                throw new IOException("La versión nueva terminó antes de confirmar su arranque.");
            }
            try { await ready.ConfigureAwait(false); }
            catch (OperationCanceledException ex) when (deadline.IsCancellationRequested)
            { throw new TimeoutException("La versión nueva no confirmó su arranque dentro de dos minutos.", ex); }
        }
        finally
        {
            deadline.Cancel();
            try { await ready.ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OperationCanceledException) { }
        }
    }

    private static async Task ReadReadyAsync(NamedPipeServerStream pipe, string session, int expectedProcess, CancellationToken token)
    {
        await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var client) || client != expectedProcess)
            throw new UnauthorizedAccessException("El acuse procede de otro proceso.");
        if (!SystemActionHost.IsLocalPipeClient(pipe.SafePipeHandle))
            throw new UnauthorizedAccessException("El acuse requiere una conexión local.");
        var bytes = new byte[16];
        await pipe.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(bytes, Guid.ParseExact(session, "N").ToByteArray()))
            throw new InvalidDataException("El acuse pertenece a otra transacción de actualización.");
    }

    internal static ProcessStartInfo StartInfo(string executable, string session)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
        start.ArgumentList.Add(Argument); start.ArgumentList.Add(session);
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return start;
    }

    internal static async Task RestartAndConfirmAsync(string executable)
    {
        var session = Guid.NewGuid().ToString("N");
        using var pipe = CreatePipe(session);
        using var process = Process.Start(StartInfo(executable, session))
            ?? throw new IOException("No se pudo iniciar Silen; se restaurará la versión anterior.");
        using var lifetime = new CancellationTokenSource();
        Task exited = Task.CompletedTask;
        try
        {
            exited = process.WaitForExitAsync(lifetime.Token);
            await ConfirmAsync(pipe, session, process.Id, exited, StartupTimeout).ConfigureAwait(false);
            if (process.HasExited) throw new IOException("Silen terminó durante la confirmación del arranque.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or TimeoutException or Win32Exception or OperationCanceledException)
        {
            var mayBeRunning = true;
            try { mayBeRunning = !process.HasExited; }
            catch (Exception query) when (query is Win32Exception or InvalidOperationException) { }
            throw new AppUpdateStartupException("No se confirmó el arranque de la versión nueva.", mayBeRunning, ex);
        }
        finally
        {
            lifetime.Cancel();
            try { await exited.ConfigureAwait(false); }
            catch (Exception ex) when (ex is OperationCanceledException or Win32Exception or InvalidOperationException) { }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out int processId);
}
