namespace Silen.Windows;

internal static class AppUpdateInstaller
{
    internal static async Task ApplyAsync(string archive, string installation, Func<string, Task> restart, CancellationToken token, Version? expectedVersion = null, string? expectedOriginalSha256 = null)
    {
        installation = AppUpdatePaths.Validate(installation);
        if (!Directory.Exists(installation)) throw new InvalidDataException("La instalación original ya no existe.");
        // Shared with earlier versions so both helpers serialize installation changes.
        var lockPath = AppUpdatePaths.Within(installation, Path.Combine(installation, ".flyy-update.lock"));
        using var installationLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        var transaction = AppUpdatePaths.Within(installation, Path.Combine(installation, ".silen-update-" + Guid.NewGuid().ToString("N")));
        var stage = Path.Combine(transaction, "stage");
        var backup = Path.Combine(transaction, "backup");
        var executable = Path.Combine(installation, "Silen.exe");
        var resources = Path.Combine(installation, "Archivos");
        var backedExe = false;
        var backedResources = false;
        var placedExe = false;
        var placedResources = false;
        var preserveBackup = false;
        Directory.CreateDirectory(stage);
        Directory.CreateDirectory(backup);
        try
        {
            await AppUpdateArchive.ExtractAsync(archive, stage, token).ConfigureAwait(false);
            if (expectedVersion is not null)
            {
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(stage, "Silen.exe"));
                var actual = new Version(Math.Max(0, info.FileMajorPart), Math.Max(0, info.FileMinorPart), Math.Max(0, info.FileBuildPart), Math.Max(0, info.FilePrivatePart));
                if (actual != AppUpdateTrust.Normalize(expectedVersion)) throw new InvalidDataException("La versión del ejecutable no coincide con la release oficial.");
            }
            ValidateExisting(executable, resources);
            token.ThrowIfCancellationRequested();
            if (expectedOriginalSha256 is not null)
            {
                using var original = File.OpenRead(executable);
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original));
                if (!hash.Equals(expectedOriginalSha256, StringComparison.Ordinal)) throw new InvalidDataException("Otro proceso cambió la instalación original; vuelve a buscar actualizaciones.");
            }
            try
            {
                File.Move(executable, Path.Combine(backup, "Silen.exe"));
                backedExe = true;
                if (Directory.Exists(resources)) { Directory.Move(resources, Path.Combine(backup, "Archivos")); backedResources = true; }
                File.Move(Path.Combine(stage, "Silen.exe"), executable);
                placedExe = true;
                Directory.Move(Path.Combine(stage, "Archivos"), resources);
                placedResources = true;
                // El callback solo termina cuando el proceso nuevo acusa un arranque
                // correcto. La copia anterior permanece durante toda la espera.
                preserveBackup = true;
                await File.WriteAllTextAsync(Path.Combine(transaction, "arranque-pendiente.txt"),
                    "La actualización todavía no confirmó su arranque. La versión anterior y sus recursos están en backup. No elimines esta carpeta para intentar recuperar Silen.", CancellationToken.None).ConfigureAwait(false);
                await restart(executable).ConfigureAwait(false);
                preserveBackup = false;
            }
            catch (AppUpdateStartupException ex) when (ex.ProcessMayBeRunning)
            {
                preserveBackup = true;
                throw new IOException($"No se confirmó el arranque de Silen. El proceso nuevo puede seguir abierto; no se sobrescribió. La versión anterior se conserva en {backup}.", ex);
            }
            catch (Exception original)
            {
                try
                {
                    if (placedResources) Directory.Delete(resources, true);
                    if (placedExe) File.Delete(executable);
                    if (backedResources) Directory.Move(Path.Combine(backup, "Archivos"), resources);
                    if (backedExe) File.Move(Path.Combine(backup, "Silen.exe"), executable);
                    preserveBackup = false;
                }
                catch (Exception rollback)
                {
                    preserveBackup = true;
                    throw new IOException($"No se pudo restaurar completamente la instalación; conserva la copia anterior en {backup}.", new AggregateException(original, rollback));
                }
                if (original is AppUpdateStartupException)
                    throw new IOException("La versión nueva terminó sin confirmar el arranque. Se restauró la versión anterior.", original);
                throw;
            }
        }
        finally
        {
            if (!preserveBackup)
            {
                try { Directory.Delete(transaction, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
    private static void ValidateExisting(string executable, string resources)
    {
        AppUpdatePaths.Validate(executable);
        using (File.Open(executable, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        if (!Directory.Exists(resources)) return;
        AppUpdatePaths.Validate(resources);
        foreach (var path in Directory.EnumerateFileSystemEntries(resources)) Check(path);
        static void Check(string path)
        {
            AppUpdatePaths.Validate(path);
            if (Directory.Exists(path)) { foreach (var child in Directory.EnumerateFileSystemEntries(path)) Check(child); }
            else { using var handle = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
        }
    }
}
