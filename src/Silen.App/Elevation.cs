using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using Silen.Windows;

namespace Silen.App;

internal static class Elevation
{
    internal static void OpenSystemProtection()
    {
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "SystemPropertiesProtection.exe")) { UseShellExecute = true });
    }
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static async Task<SystemActionResponse> ExecuteSystemActionAsync(SystemActionRequest request, CancellationToken cancellationToken)
    {
        if (IsElevated()) return new(new(false, "La interfaz debe ejecutarse sin administrador. Vuelve a abrir Silen de forma normal."));
        var bytes = SystemActionPolicy.Serialize(request);
        var session = Guid.NewGuid().ToString("N");
        using var pipe = SystemActionHost.CreateSessionPipe(session);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SystemActionPolicy.SessionTimeout);
        Process? helper = null;
        try
        {
            var start = CreateStartInfo();
            start.ArgumentList.Add(SystemActionHost.Argument);
            start.ArgumentList.Add(session);
            start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.ArgumentList.Add(Convert.ToHexString(SHA256.HashData(bytes)));
            helper = Process.Start(start) ?? throw new IOException("Windows no inició el ejecutor autorizado.");
            return await SystemActionHost.ExchangeAsync(pipe, helper, bytes, timeout.Token);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { return new(new(false, "Se canceló la autorización de Windows. No se inició la operación del sistema."),
            RestorePoint: request.Action == SystemActionKind.CreateRestorePoint ? new(RestorePointState.Cancelled) : null); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(new(false, "El ejecutor agotó el tiempo de la sesión. Revisa los respaldos antes de reintentar.")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        { return new(new(false, "No se pudo completar la operación autorizada. " + ex.Message)); }
        finally
        {
            // Cerrar el canal cancela el token del helper: no depende de que una UI normal pueda matar un proceso elevado.
            pipe.Dispose();
            if (helper is not null)
            {
                try { await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (Exception ex) when (ex is TimeoutException or Win32Exception or InvalidOperationException or NotSupportedException) { }
                helper.Dispose();
            }
        }
    }

    public static async Task<Silen.Core.DriverDownloadResult> DownloadDriverUpdatesAsync(IReadOnlyList<Silen.Core.DriverUpdateInfo> updates, CancellationToken cancellationToken)
    {
        if (updates.Count is 0 or > 20 || updates.Any(update => !Guid.TryParse(update.UpdateId, out _)))
            return new(false, [], "Selecciona hasta 20 ofertas vigentes de Windows Update.");
        var request = new SystemActionRequest(SystemActionKind.DownloadDrivers, Updates: updates.Select(update => new SystemDriverUpdateIdentity(Guid.Parse(update.UpdateId), update.Revision)).ToArray());
        try
        {
            var response = await ExecuteSystemActionAsync(request, cancellationToken);
            return response.Download ?? new(false, [], response.Outcome.Message);
        }
        catch (ArgumentException) { return new(false, [], "Las identidades seleccionadas son inválidas o están repetidas. Vuelve a buscar actualizaciones."); }
    }

    internal static ProcessStartInfo CreateStartInfo()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "Silen.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("No se encontró Silen.exe en su carpeta. Vuelve a extraer el paquete completo de Silen.");
        return new ProcessStartInfo { FileName = executable, WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
    }

    public static bool TryOpenWithoutAdministrator(bool drivers)
    {
        // La instancia existente de Explorer conserva el token normal del usuario.
        object? shell = null;
        object? windows = null;
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null) return false;
            shell = Activator.CreateInstance(type);
            dynamic automation = shell!;
            windows = automation.Windows();
            dynamic desktop = ((dynamic)windows).FindWindowSW(0, 0, 8, out int _, 1);
            desktop.Document.Application.ShellExecute(Path.Combine(AppContext.BaseDirectory, "Silen.exe"), drivers ? "--normal-ui --drivers" : "--normal-ui", AppContext.BaseDirectory, "open", 1);
            return true;
        }
        catch { return false; }
        finally
        {
            if (windows is not null && System.Runtime.InteropServices.Marshal.IsComObject(windows)) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(windows);
            if (shell is not null && System.Runtime.InteropServices.Marshal.IsComObject(shell)) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    // Compatibilidad de llamadas antiguas: nunca iniciar la interfaz completa como administrador.
    public static void OpenElevated() => throw new InvalidOperationException("Autoriza la operación concreta desde Ajustes; la interfaz continúa sin administrador.");
    public static void OpenElevatedDrivers() => throw new InvalidOperationException("La instalación de drivers abre su instalador oficial; Silen continúa sin administrador.");
}
