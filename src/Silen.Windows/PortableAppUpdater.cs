using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace Silen.Windows;

internal sealed record AppUpdateJob(string Installation, string ArchivePath, AppRelease Release, int ParentId, long ParentStartUtcTicks, string OriginalSha256, string InstalledVersion, DateTimeOffset CreatedUtc);

public static class PortableAppUpdater
{
    internal static void RequireUnelevated(bool elevated)
    {
        if (elevated) throw new InvalidOperationException("Cierra Silen y vuelve a abrirlo sin permisos de administrador para actualizar, o instala la versión portable manualmente.");
    }
    private static void RequireUnelevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        RequireUnelevated(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator));
    }
    public static ProcessStartInfo CreateStartInfo(PreparedAppUpdate update)
    {
        RequireUnelevated();
        using var current = Process.GetCurrentProcess();
        return CreateStartInfo(update, AppUpdatePaths.CacheRoot, Environment.ProcessPath ?? throw new InvalidDataException("No se pudo identificar el ejecutable actual."),
            current.Id, current.StartTime.ToUniversalTime().Ticks, Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0));
    }
    public static Task ApplyFromJobAsync(string jobPath, CancellationToken cancellationToken = default)
    {
        RequireUnelevated();
        var service = new AppUpdateService();
        return ApplyJobAsync(jobPath, Environment.ProcessPath ?? "", AppUpdatePaths.CacheRoot, service.ReadTagAsync, WaitForParentAsync, RestartAsync, cancellationToken);
    }
    internal static ProcessStartInfo CreateStartInfo(PreparedAppUpdate update, string cacheRoot, string executable, int parentId, long startTicks, Version installedVersion)
    {
        AppUpdateTrust.Validate(update.Release);
        cacheRoot = AppUpdatePaths.Validate(cacheRoot);
        executable = AppUpdatePaths.Validate(executable);
        if (!Path.GetFileName(executable).Equals("Silen.exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(executable))
            throw new InvalidDataException("La actualización requiere el ejecutable portable Silen.exe.");
        var installation = Path.GetDirectoryName(executable)!;
        ValidateInstallation(installation, cacheRoot);
        if (update.Release.Version <= AppUpdateTrust.Normalize(installedVersion)) throw new InvalidDataException("La actualización no es más nueva que la instalación.");
        var archive = AppUpdatePaths.Within(cacheRoot, update.ArchivePath);
        if (!archive.Equals(AppUpdatePaths.Archive(cacheRoot, update.Release), StringComparison.OrdinalIgnoreCase) || !File.Exists(archive) || new FileInfo(archive).Length != update.Release.Size)
            throw new InvalidDataException("El paquete no pertenece a la caché de actualizaciones verificadas.");
        if (parentId <= 0 || startTicks <= 0) throw new InvalidDataException("No se pudo identificar el proceso original.");
        AppUpdateCache.Prune(cacheRoot, archive);
        var directory = AppUpdatePaths.Within(cacheRoot, Path.Combine(cacheRoot, "jobs", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        var helper = Path.Combine(directory, "Silen.exe");
        var jobPath = Path.Combine(directory, "job.bin");
        try
        {
            File.Copy(executable, helper, false);
            var job = new AppUpdateJob(installation, archive, update.Release, parentId, startTicks, Hash(helper), installedVersion.ToString(), DateTimeOffset.UtcNow);
            File.WriteAllBytes(jobPath, AppUpdateJobProtection.Protect(JsonSerializer.SerializeToUtf8Bytes(job)));
            var start = new ProcessStartInfo(helper) { UseShellExecute = false, WorkingDirectory = directory, CreateNoWindow = true };
            start.ArgumentList.Add("--apply-update");
            start.ArgumentList.Add(jobPath);
            return start;
        }
        catch { Directory.Delete(directory, true); throw; }
    }
    internal static async Task ApplyJobAsync(string jobPath, string helper, string cacheRoot, Func<string, CancellationToken, Task<AppRelease>> resolve, Func<AppUpdateJob, CancellationToken, Task> wait, Func<string, Task> restart, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var job = ReadJob(jobPath, helper, cacheRoot);
        var trusted = await resolve(job.Release.Tag, token).ConfigureAwait(false);
        if (trusted != job.Release) throw new InvalidDataException("La release oficial cambió; vuelve a descargar la actualización.");
        await using var archiveLock = new FileStream(job.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (!await AppUpdateService.VerifyArchiveAsync(job.ArchivePath, trusted, token).ConfigureAwait(false))
            throw new InvalidDataException("El paquete de actualización ya no coincide con su SHA-256 oficial.");
        await wait(job, token).ConfigureAwait(false);
        AppUpdatePaths.Validate(job.Installation);
        if (!Hash(Path.Combine(job.Installation, "Silen.exe")).Equals(job.OriginalSha256, StringComparison.Ordinal))
            throw new InvalidDataException("La instalación original cambió mientras se esperaba el cierre.");
        await AppUpdateInstaller.ApplyAsync(job.ArchivePath, job.Installation, restart, token, trusted.Version, job.OriginalSha256).ConfigureAwait(false);
    }
    private static AppUpdateJob ReadJob(string jobPath, string helper, string cacheRoot)
    {
        cacheRoot = AppUpdatePaths.Validate(cacheRoot);
        jobPath = AppUpdatePaths.Within(Path.Combine(cacheRoot, "jobs"), jobPath);
        helper = AppUpdatePaths.Within(cacheRoot, helper);
        var directory = Path.GetDirectoryName(jobPath)!;
        if (Path.GetFileName(jobPath) != "job.bin" || !Guid.TryParseExact(Path.GetFileName(directory), "N", out _) ||
            !Path.GetDirectoryName(directory)!.Equals(Path.Combine(cacheRoot, "jobs"), StringComparison.OrdinalIgnoreCase) ||
            !helper.Equals(Path.Combine(directory, "Silen.exe"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("El ayudante no pertenece al trabajo de actualización.");
        using var file = new FileStream(jobPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is <= 0 or > 32768) throw new InvalidDataException("El trabajo de actualización supera el límite permitido.");
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        AppUpdateJob job;
        try { job = JsonSerializer.Deserialize<AppUpdateJob>(AppUpdateJobProtection.Unprotect(bytes), new JsonSerializerOptions { MaxDepth = 16 }) ?? throw new InvalidDataException("El trabajo está vacío."); }
        catch (JsonException ex) { throw new InvalidDataException("El trabajo de actualización no es válido.", ex); }
        if (job.Release is null || job.Release.Version is null || job.Release.Tag is null || job.Release.Sha256 is null || job.InstalledVersion is null || job.Installation is null || job.ArchivePath is null ||
            job.ParentId <= 0 || job.ParentStartUtcTicks <= 0 || job.CreatedUtc > DateTimeOffset.UtcNow.AddMinutes(1) || job.CreatedUtc < DateTimeOffset.UtcNow.AddHours(-2))
            throw new InvalidDataException("El trabajo no tiene una identidad válida o ha caducado.");
        AppUpdateTrust.Validate(job.Release);
        ValidateInstallation(job.Installation, cacheRoot);
        var archive = AppUpdatePaths.Within(cacheRoot, job.ArchivePath);
        if (archive != AppUpdatePaths.Archive(cacheRoot, job.Release) || !Version.TryParse(job.InstalledVersion, out var installed) || job.Release.Version <= AppUpdateTrust.Normalize(installed) ||
            Hash(helper) != job.OriginalSha256 || Hash(Path.Combine(job.Installation, "Silen.exe")) != job.OriginalSha256)
            throw new InvalidDataException("El trabajo no corresponde al ayudante, paquete o instalación original.");
        return job;
    }
    internal static async Task WaitForParentAsync(AppUpdateJob job, CancellationToken token)
    {
        Process parent;
        try { parent = Process.GetProcessById(job.ParentId); }
        catch (ArgumentException) { return; }
        using (parent)
        {
            try
            {
                if (parent.StartTime.ToUniversalTime().Ticks != job.ParentStartUtcTicks ||
                    !string.Equals(parent.MainModule?.FileName, Path.Combine(job.Installation, "Silen.exe"), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("El PID original pertenece a otro proceso; la actualización fue cancelada.");
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromMinutes(2));
                await parent.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (parent.HasExited) { }
        }
    }
    private static void ValidateInstallation(string installation, string cacheRoot)
    {
        installation = AppUpdatePaths.Validate(installation);
        var dataRoot = Path.GetDirectoryName(AppUpdatePaths.CacheRoot)!;
        if (Overlaps(installation, cacheRoot) || Overlaps(installation, dataRoot))
            throw new InvalidDataException("La instalación no puede coincidir con los informes ni la caché de actualizaciones.");
        static bool Overlaps(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase) || a.StartsWith(b.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) || b.StartsWith(a.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    }
    private static string Hash(string path)
    {
        AppUpdatePaths.Validate(path);
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }
    private static Task RestartAsync(string executable) => AppUpdateStartup.RestartAndConfirmAsync(executable);
}
