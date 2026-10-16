using System.ComponentModel;
using Silen.Core;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Silen.Windows;

public sealed class FortniteGameService : IFortniteGameService
{
    private const string ExecutableRelativePath = "FortniteGame/Binaries/Win64/FortniteClient-Win64-Shipping.exe";
    private sealed record Detection(string Install, string Executable, long ExecutableLength, long ExecutableWrite, string? SettingsHash);
    private readonly string manifestDirectory;
    private readonly string settingsPath;
    private readonly string[] fallbackDirectories;
    private readonly Func<bool> isRunning;
    private readonly Func<DateTimeOffset> clock;
    private readonly FortniteGameJournal journal;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Detection? detection;

    public FortniteGameService() : this(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests"),
        DefaultSettingsPath(),
        Path.Combine(AppDataPaths.Root, "fortnite"),
        DefaultFallbacks(), IsGameRunning) { }

    internal FortniteGameService(string manifestDirectory, string settingsPath, string journalDirectory,
        IEnumerable<string> fallbackDirectories, Func<bool> isRunning, Func<DateTimeOffset>? clock = null)
    {
        this.manifestDirectory = FortniteSafeFiles.LocalPath(manifestDirectory);
        this.settingsPath = FortniteSafeFiles.LocalPath(settingsPath);
        this.fallbackDirectories = fallbackDirectories.Take(32).ToArray();
        this.isRunning = isRunning;
        this.clock = clock ?? (() => DateTimeOffset.Now);
        journal = new FortniteGameJournal(journalDirectory, this.settingsPath);
    }

    public Task<FortniteInstallation> DetectAsync(string? installDirectory = null, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                detection = null;
                var running = isRunning();
                var rejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var candidates = installDirectory is null ? FindCandidates(rejected, cancellationToken) : [installDirectory];
                foreach (var candidate in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var directory = FortniteSafeFiles.LocalPath(candidate);
                        if (rejected.Contains(directory)) continue;
                        var executable = Path.Combine(directory, ExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar));
                        using var exe = FortniteSafeFiles.Open(executable, false, exclusive: false, maximumBytes: int.MaxValue);
                        string? settingsHash = null;
                        string? offeredSettings = null;
                        var message = "Fortnite detectado. Se conservan el renderizador, la resolución y el límite de FPS.";
                        try
                        {
                            using var file = FortniteSafeFiles.Open(settingsPath, false);
                            if (file.IsReadOnly) message = "Fortnite detectado; su configuración es de solo lectura. Quita ese atributo y vuelve a detectar antes de editar.";
                            else { settingsHash = FortniteGameJournal.Hash(file.ReadBytes()); offeredSettings = settingsPath; }
                        }
                        catch (Exception ex) when (IsFileFailure(ex))
                        {
                            message = "Fortnite detectado; su configuración aún no está disponible, está ocupada o no es compatible. Abre y cierra el juego y vuelve a detectar.";
                        }
                        detection = new(directory, executable, exe.Length, exe.LastWriteTicks, settingsHash);
                        if (running) message = "Fortnite está abierto. Ciérralo antes de cambiar su configuración.";
                        return new FortniteInstallation(true, directory, executable, offeredSettings, running, message);
                    }
                    catch (Exception ex) when (IsFileFailure(ex)) { }
                }
                return new FortniteInstallation(false, null, null, null, running,
                    "No se encontró una instalación completa de Fortnite. Puedes seleccionar su carpeta; no se recorren todos los archivos del disco.");
            }
            finally { gate.Release(); }
        }, cancellationToken);

    public Task<OptimizationOutcome> ApplyCompetitiveSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (detection is null) return Failure("Detecta Fortnite y revisa su carpeta antes de aplicar.");
                if (detection.SettingsHash is null) return Failure("No hay configuración revisada disponible. Abre y cierra Fortnite y vuelve a detectar.");
                if (isRunning()) return Failure("Cierra Fortnite antes de aplicar el perfil.");
                using var lease = await journal.LeaseAsync(cancellationToken);
                var entries = journal.Read();
                using var executable = FortniteSafeFiles.Open(detection.Executable, false, exclusive: false, maximumBytes: int.MaxValue);
                if (executable.Length != detection.ExecutableLength || executable.LastWriteTicks != detection.ExecutableWrite)
                    return Failure("La instalación de Fortnite cambió. Vuelve a detectarla.");
                using var settings = FortniteSafeFiles.Open(settingsPath, true);
                var original = settings.ReadBytes();
                var currentHash = FortniteGameJournal.Hash(original);
                Recover(entries, (original, settings.Identity, currentHash));
                if (!currentHash.Equals(detection.SettingsHash, StringComparison.Ordinal))
                    return Failure("La configuración cambió desde la revisión. Vuelve a detectar Fortnite.");
                var transformed = FortniteSettingsEditor.Transform(original);
                if (transformed.Recognized == 0) return Failure("No se encontraron ajustes compatibles existentes. No se agregó ninguna clave.");
                if (transformed.Changed == 0) return new OptimizationOutcome(true, "Los ajustes compatibles ya están en el perfil competitivo.");
                cancellationToken.ThrowIfCancellationRequested();
                var change = new FortniteGameChange
                {
                    SettingsPath = settingsPath, PreviousHash = currentHash, IntendedHash = FortniteGameJournal.Hash(transformed.Bytes),
                    AppliedAt = clock(), ModifiedKeys = transformed.Changed, FileIdentity = settings.Identity
                };
                journal.Backup(change, original, transformed.Bytes);
                entries.Add(change);
                journal.Write(entries);
                cancellationToken.ThrowIfCancellationRequested();
                if (isRunning())
                {
                    change.State = "NotApplied";
                    journal.Write(entries);
                    return Failure("Fortnite se abrió durante la revisión. Ciérralo y vuelve a aplicar.");
                }
                FortniteSafeFiles.WriteLocked(settings, transformed.Bytes);
                change.State = "Completed";
                journal.Write(entries);
                detection = detection with { SettingsHash = change.IntendedHash };
                return new OptimizationOutcome(true,
                    $"Perfil competitivo aplicado a {change.ModifiedKeys} valores existentes. Se guardó un respaldo para deshacer; revisa el resultado en el juego.", true);
            }
            catch (Exception ex) when (IsFileFailure(ex)) { return Failure("No se modificó o no se pudo confirmar la configuración: " + ex.Message + " Revisa el historial antes de reintentar."); }
            finally { gate.Release(); }
        }, cancellationToken);

    public Task<List<AppliedOptimization>> ReadChangesAsync(CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                using var lease = await journal.LeaseAsync(cancellationToken);
                var entries = journal.Read();
                var current = ReadCurrentState();
                Recover(entries, current);
                return entries.Select(entry => new AppliedOptimization("fortnite:" + entry.Id, "Fortnite · perfil competitivo", entry.AppliedAt,
                    current is not null && CanRestore(entry, current.Value),
                    entry.State switch
                    {
                        "Completed" => $"{entry.ModifiedKeys} valores de calidad, VSync, desenfoque o FPS. Respaldo completo de la configuración.",
                        "Undone" => "Configuración original restaurada.",
                        "NotApplied" => "La operación no llegó a cambiar la configuración.",
                        "Partial" or "UndoPartial" => "Escritura interrumpida reconocida en el mismo archivo. El respaldo permite restaurar los bytes originales.",
                        "Conflict" => "La configuración cambió fuera de Silen. Se conserva el respaldo y no se sobrescribe.",
                        _ => "Operación pendiente; no se pudo confirmar el archivo. Se conserva el respaldo."
                    })).Reverse().ToList();
            }
            finally { gate.Release(); }
        }, cancellationToken);

    public Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (changeId is null || !changeId.StartsWith("fortnite:", StringComparison.Ordinal) ||
                    !Guid.TryParseExact(changeId[9..], "N", out _)) return Failure("El cambio de Fortnite no es válido.");
                if (isRunning()) return Failure("Cierra Fortnite antes de restaurar la configuración.");
                using var lease = await journal.LeaseAsync(cancellationToken);
                var entries = journal.Read();
                using var settings = FortniteSafeFiles.Open(settingsPath, true);
                var currentBytes = settings.ReadBytes();
                var current = (currentBytes, settings.Identity, FortniteGameJournal.Hash(currentBytes));
                Recover(entries, current);
                var change = entries.SingleOrDefault(x => "fortnite:" + x.Id == changeId);
                if (change is null || !CanRestore(change, current)) return Failure("Ese cambio ya fue deshecho o la configuración cambió fuera de Silen. Se conservaron el archivo actual y el respaldo.");
                var original = journal.ReadBackup(change);
                cancellationToken.ThrowIfCancellationRequested();
                change.State = "UndoPending";
                journal.Write(entries);
                cancellationToken.ThrowIfCancellationRequested();
                if (isRunning())
                {
                    change.State = "Completed";
                    journal.Write(entries);
                    return Failure("Fortnite se abrió durante la revisión. Ciérralo antes de restaurar.");
                }
                FortniteSafeFiles.WriteLocked(settings, original);
                change.State = "Undone";
                journal.Write(entries);
                if (detection is not null) detection = detection with { SettingsHash = change.PreviousHash };
                return new OptimizationOutcome(true, "Se restauraron exactamente los bytes originales de la configuración de Fortnite.", true);
            }
            catch (Exception ex) when (IsFileFailure(ex)) { return Failure("No se restauró o no se pudo confirmar la configuración: " + ex.Message + " Revisa el historial antes de reintentar."); }
            finally { gate.Release(); }
        }, cancellationToken);

    private List<string> FindCandidates(HashSet<string> rejected, CancellationToken token)
    {
        var candidates = new List<string>();
        try
        {
            using var directories = FortniteSafeFiles.LockDirectory(manifestDirectory);
            foreach (var path in Directory.EnumerateFiles(manifestDirectory, "*.item", SearchOption.TopDirectoryOnly).Take(128))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var file = FortniteSafeFiles.Open(path, false, maximumBytes: 256 * 1024);
                    using var document = JsonDocument.Parse(file.ReadBytes());
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("AppName", out var appName) ||
                        appName.ValueKind != JsonValueKind.String || appName.GetString() != "Fortnite" ||
                        !root.TryGetProperty("InstallLocation", out var install) || install.ValueKind != JsonValueKind.String) continue;
                    var directory = FortniteSafeFiles.LocalPath(install.GetString()!);
                    if (Flag(root, "bIsIncompleteInstall") || Flag(root, "bNeedsValidation")) rejected.Add(directory);
                    else candidates.Add(directory);
                }
                catch (Exception ex) when (IsFileFailure(ex) || ex is JsonException or InvalidOperationException) { }
            }
        }
        catch (Exception ex) when (IsFileFailure(ex)) { }
        candidates.AddRange(fallbackDirectories);
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool Flag(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.False;

    private (byte[] Bytes, string Identity, string Hash)? ReadCurrentState()
    {
        try { using var file = FortniteSafeFiles.Open(settingsPath, false); var bytes = file.ReadBytes(); return (bytes, file.Identity, FortniteGameJournal.Hash(bytes)); }
        catch (Exception ex) when (IsFileFailure(ex)) { return null; }
    }

    private void Recover(List<FortniteGameChange> entries, (byte[] Bytes, string Identity, string Hash)? current)
    {
        if (current is null) return;
        var changed = false;
        foreach (var entry in entries.Where(x => x.State is "Pending" or "UndoPending" or "Partial" or "UndoPartial"))
        {
            var undo = entry.State is "UndoPending" or "UndoPartial";
            var state = current.Value.Hash == entry.IntendedHash ? "Completed" :
                current.Value.Hash == entry.PreviousHash ? (undo ? "Undone" : "NotApplied") :
                RecognizesPartial(entry, current.Value, undo) ? (undo ? "UndoPartial" : "Partial") : "Conflict";
            changed |= entry.State != state;
            entry.State = state;
        }
        if (changed) journal.Write(entries);
    }

    private bool CanRestore(FortniteGameChange entry, (byte[] Bytes, string Identity, string Hash) current) =>
        entry.State == "Completed" && current.Hash == entry.IntendedHash ||
        entry.State is "Partial" or "UndoPartial" && RecognizesPartial(entry, current, entry.State == "UndoPartial");

    private bool RecognizesPartial(FortniteGameChange entry, (byte[] Bytes, string Identity, string Hash) current, bool undo)
    {
        if (current.Identity != entry.FileIdentity) return false;
        try
        {
            var original = journal.ReadBackup(entry);
            var intended = journal.ReadBackup(entry, true);
            return IsPrefixWrite(current.Bytes, undo ? intended : original, undo ? original : intended);
        }
        catch (Exception ex) when (IsFileFailure(ex)) { return false; }
    }

    private static bool IsPrefixWrite(byte[] current, byte[] before, byte[] target)
    {

        if (current.Length < before.Length || current.Length > Math.Max(before.Length, target.Length)) return false;
        var prefix = 0;
        while (prefix < Math.Min(current.Length, target.Length) && current[prefix] == target[prefix]) prefix++;
        if (prefix == current.Length) return true;
        return current.Length == before.Length && current.AsSpan(prefix).SequenceEqual(before.AsSpan(prefix));
    }

    private static OptimizationOutcome Failure(string message) => new(false, message);
    private static bool IsFileFailure(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException
        or Win32Exception or System.Security.SecurityException or DecoderFallbackException or EncoderFallbackException;

    private static string DefaultSettingsPath()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Config");
        var client = Path.Combine(root, "WindowsClient", "GameUserSettings.ini");
        var windows = Path.Combine(root, "Windows", "GameUserSettings.ini");
        return File.Exists(client) || !File.Exists(windows) ? client : windows;
    }

    private static IEnumerable<string> DefaultFallbacks()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games", "Fortnite");
        foreach (var drive in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed).Take(10))
        {
            yield return Path.Combine(drive.RootDirectory.FullName, "Epic Games", "Fortnite");
            yield return Path.Combine(drive.RootDirectory.FullName, "Games", "Fortnite");
            yield return Path.Combine(drive.RootDirectory.FullName, "Fortnite");
        }
    }

    private static bool IsGameRunning()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try { if (process.ProcessName.StartsWith("FortniteClient", StringComparison.OrdinalIgnoreCase)) return true; }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
            }
        }
        return false;
    }
}
