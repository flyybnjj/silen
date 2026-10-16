using Silen.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Silen.Gaming;

public sealed class GamingCapture
{

    public async Task<GamingSession> CaptureAsync(string executable, int processId, int durationSeconds, string outputPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId), "El PID debe ser positivo.");
        if (durationSeconds is < 15 or > 300) throw new ArgumentOutOfRangeException(nameof(durationSeconds), "La captura debe durar entre 15 y 300 segundos.");
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var executablePath = Path.GetFullPath(executable);
        var destination = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selecciona el ejecutable .exe de consola de PresentMon oficial.", nameof(executable));
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("No se encontró el colector gaming incluido. Vuelve a extraer el paquete completo de Silen para recuperarlo.", executablePath);
        if (!string.Equals(Path.GetExtension(destination), ".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("El archivo de salida debe tener extensión .csv.", nameof(outputPath));
        if (File.Exists(destination) || Directory.Exists(destination))
            throw new IOException("El archivo de salida ya existe. Elige un nombre nuevo para conservarlo.");
        var outputDirectory = Path.GetDirectoryName(destination)!;
        if (!Directory.Exists(outputDirectory)) throw new DirectoryNotFoundException("La carpeta de salida no existe.");



        var temporaryCsv = Path.Combine(outputDirectory, ".Silen-" + Guid.NewGuid().ToString("N") + ".csv");
        var sessionName = "Silen-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "--process_id", processId.ToString(CultureInfo.InvariantCulture),
            "--timed", durationSeconds.ToString(CultureInfo.InvariantCulture),
            "--terminate_after_timed", "--no_console_stats", "--v1_metrics",
            "--session_name", sessionName,
            "--output_file", temporaryCsv
        }) start.ArgumentList.Add(argument);

        using var collector = new Process { StartInfo = start };
        var started = false;
        var forcedTermination = false;
        try
        {
            try { started = collector.Start(); }
            catch (Win32Exception error)
            {
                throw new InvalidOperationException("No se pudo iniciar la consola de PresentMon. Comprueba su origen oficial, la compatibilidad y los permisos; también puedes importar un CSV.", error);
            }
            if (!started) throw new InvalidOperationException("No se pudo iniciar PresentMon.");


            var stdout = DrainAsync(collector.StandardOutput);
            var stderr = DrainAsync(collector.StandardError);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds + 30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                await collector.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                forcedTermination = await StopOwnCollectorAsync(collector).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                throw new TimeoutException("PresentMon no terminó dentro del tiempo de captura y 30 segundos de margen. Se detuvo únicamente el colector iniciado por Silen.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (collector.ExitCode != 0)
            {
                var details = (await stderr.ConfigureAwait(false)).Trim();
                if (details.Length == 0) details = (await stdout.ConfigureAwait(false)).Trim();
                throw new InvalidOperationException($"El colector gaming terminó con código {collector.ExitCode}. Comprueba los permisos ETW de Windows y que el proceso siga activo. {details}");
            }
            if (!File.Exists(temporaryCsv)) throw new InvalidDataException("PresentMon no generó un CSV. Comprueba que el PID siga activo y esté presentando frames.");
            var session = await Task.Run(() => PresentMonCsv.Parse(temporaryCsv, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryCsv, destination, overwrite: false);
            return session with { SourceFile = destination };
        }
        finally
        {
            try
            {
                if (started) forcedTermination |= await StopOwnCollectorAsync(collector).ConfigureAwait(false);
                if (forcedTermination) await StopOwnTraceSessionAsync(executablePath, sessionName).ConfigureAwait(false);
            }
            finally
            {
                try { File.Delete(temporaryCsv); }
                catch (IOException) {  }
                catch (UnauthorizedAccessException) {  }
            }
        }
    }

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        const int maximumDiagnosticCharacters = 4096;
        var retained = new StringBuilder();
        var buffer = new char[1024];
        int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            var remaining = maximumDiagnosticCharacters - retained.Length;
            if (remaining > 0) retained.Append(buffer, 0, Math.Min(count, remaining));
        }
        return retained.ToString();
    }

    private static async Task<bool> StopOwnCollectorAsync(Process collector)
    {
        try
        {
            if (collector.HasExited) return false;
            collector.Kill(entireProcessTree: false);
            await collector.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            return true;
        }
        catch (InvalidOperationException) {  }
        catch (Win32Exception error) { throw new InvalidOperationException("No se pudo detener el colector de PresentMon iniciado por Silen.", error); }
        return false;
    }



    private static async Task StopOwnTraceSessionAsync(string executable, string sessionName)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("--terminate_existing_session");
        start.ArgumentList.Add("--session_name");
        start.ArgumentList.Add(sessionName);
        using var cleanup = new Process { StartInfo = start };
        var started = false;
        try
        {
            started = cleanup.Start();
            if (!started) throw new InvalidOperationException("No se pudo iniciar la limpieza de la sesión ETW de PresentMon.");
            var stdout = DrainAsync(cleanup.StandardOutput);
            var stderr = DrainAsync(cleanup.StandardError);
            await cleanup.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            var details = ((await stderr.ConfigureAwait(false)) + " " + (await stdout.ConfigureAwait(false))).Trim();
            if (cleanup.ExitCode != 0 && !details.Contains("no existing sessions found", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"No se pudo cerrar la sesión ETW {sessionName}. PresentMon terminó con código {cleanup.ExitCode}: {details}");
        }
        catch (Win32Exception error)
        {
            throw new InvalidOperationException("No se pudo iniciar PresentMon para cerrar su sesión ETW " + sessionName + ".", error);
        }
        catch (TimeoutException error)
        {
            throw new InvalidOperationException("PresentMon no cerró a tiempo su sesión ETW " + sessionName + ".", error);
        }
        finally
        {
            if (started) await StopOwnCollectorAsync(cleanup).ConfigureAwait(false);
        }
    }
}
