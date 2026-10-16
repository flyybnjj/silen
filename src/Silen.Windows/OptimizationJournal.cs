using System.Text.Json;
using System.Text.Json.Serialization;

namespace Silen.Windows;

internal sealed record OptimizationValueChange(string Kind, string Key, string Name, RegistryValueState Previous, RegistryValueState Intended);
internal sealed class OptimizationJournalEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ToolId { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTimeOffset AppliedAt { get; set; }
    public string State { get; set; } = "Pending";
    public List<OptimizationValueChange> Values { get; set; } = [];
}

internal sealed class OptimizationJournal
{
    internal const int MaximumBytes = 16 * 1024 * 1024;
    private readonly string directory;
    private readonly OptimizationAuthority authority;
    internal IReadOnlyList<OptimizationJournalEntry> UntrustedLegacyEntries { get; private set; } = [];
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private static readonly JsonSerializerOptions CompactOptions = new(Options) { WriteIndented = false };

    internal OptimizationJournal(string directory, OptimizationAuthority authority = OptimizationAuthority.User)
    {
        this.directory = Path.GetFullPath(directory);
        this.authority = authority;
    }

    internal static bool IsMachineValue(OptimizationValueChange value) =>
        value.Kind is "ServiceStart" or "ServiceRunning" ||
        value.Kind == "Registry" && value.Key.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase);

    internal void DemandAuthority(OptimizationValueChange value)
    {
        if (authority == OptimizationAuthority.User && IsMachineValue(value) ||
            authority == OptimizationAuthority.Machine && !IsMachineValue(value))
            throw new UnauthorizedAccessException("El respaldo no tiene autoridad para este ámbito de Windows.");
    }

    internal async Task<FileStream> LeaseAsync(CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var lockPath = Path.Combine(directory, "optimizations.lock");
                var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                try
                {
                    if (authority == OptimizationAuthority.Machine) ProtectedOptimizationDirectory.ProtectNewFile(stream);
                    return stream;
                }
                catch { stream.Dispose(); throw; }
            }
            catch (IOException ex) when (ex.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021))
            {
                if (DateTime.UtcNow >= deadline) throw new IOException("Otra instancia está aplicando una mejora. Vuelve a intentarlo.", ex);
                await Task.Delay(25, token);
            }
        }
    }

    internal async Task<List<OptimizationJournalEntry>> ReadAsync(CancellationToken token)
    {
        var path = Path.Combine(directory, "optimizations.json");
        if (!File.Exists(path)) return [];
        try
        {
            var file = new FileInfo(path);
            if (file.Length > MaximumBytes) throw new InvalidDataException("El respaldo de mejoras supera 16 MiB. Se conservó íntegro; no se aplicarán nuevos cambios hasta revisar su almacenamiento.");
            var entries = JsonSerializer.Deserialize<List<OptimizationJournalEntry>>(await File.ReadAllTextAsync(path, token), Options);
            if (entries is null || entries.Any(x => x is null)) throw new InvalidDataException("Respaldo de mejoras incompleto.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (!Guid.TryParseExact(entry.Id, "N", out _) || !ids.Add(entry.Id) || string.IsNullOrWhiteSpace(entry.Title) ||
                    entry.State is not ("Pending" or "Completed" or "Partial" or "UndoPending" or "Undone") ||
                    entry.Values is null || entry.Values.Count is 0 or > 16)
                    throw new InvalidDataException("Respaldo de mejoras incompatible.");
                var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var value in entry.Values)
                {
                    Validate(entry.ToolId, value);
                    if (!targets.Add(value.Kind + "\0" + value.Key + "\0" + value.Name))
                        throw new InvalidDataException("El respaldo contiene destinos duplicados.");
                }
            }
            if (authority == OptimizationAuthority.User)
            {
                UntrustedLegacyEntries = entries.Where(entry => entry.Values.Any(IsMachineValue)).ToList();
                if (UntrustedLegacyEntries.Count > 0)
                {
                    // Conserva una copia exacta antes de cualquier futura escritura del historial de usuario.
                    var quarantine = Path.Combine(directory, "optimizations.legacy-untrusted.json");
                    if (!File.Exists(quarantine)) File.Copy(path, quarantine, overwrite: false);
                    entries.RemoveAll(entry => entry.Values.Any(IsMachineValue));
                }
            }
            else if (authority == OptimizationAuthority.Machine)
                foreach (var value in entries.SelectMany(entry => entry.Values)) DemandAuthority(value);
            return entries;
        }
        catch (JsonException ex) { throw new InvalidDataException("El respaldo de mejoras está dañado. Se conservó para recuperación.", ex); }
    }

    internal async Task WriteAsync(List<OptimizationJournalEntry> entries, CancellationToken token, bool addingEntry = false,
        OptimizationJournalEntry? pendingUndo = null)
    {
        foreach (var value in entries.SelectMany(entry => entry.Values)) DemandAuthority(value);
        var path = Path.Combine(directory, "optimizations.json");
        var temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough))
            {
                if (authority == OptimizationAuthority.Machine) ProtectedOptimizationDirectory.ProtectNewFile(stream);
                await JsonSerializer.SerializeAsync(stream, entries, Options, token);
                // Reserva el crecimiento de los estados de todas las entradas para que
                // completar o deshacer un cambio nuevo no vuelva ilegible el respaldo.
                var reserve = addingEntry ? entries.Sum(entry => (long)"UndoPending".Length - entry.State.Length) : 0;
                if (!addingEntry && stream.Length > MaximumBytes)
                {
                    // Un respaldo legado legible puede no reservar los dos bytes
                    // de Completed -> UndoPending. Compactar conserva todos sus datos.
                    await RewriteCompactAsync();
                    if (stream.Length > MaximumBytes && pendingUndo?.State == "UndoPending")
                    {
                        // Pending es el estado durable genérico ya admitido por el
                        // formato anterior. El reintento de Undo valida los mismos
                        // valores Previous/Intended, sin depender de esta etiqueta.
                        pendingUndo.State = "Pending";
                        await RewriteCompactAsync();
                    }
                }
                if (stream.Length + reserve > MaximumBytes)
                    throw new InvalidDataException("El historial alcanzó su capacidad de 16 MiB. No se inició este cambio; se conservan todos los respaldos. Revisa su almacenamiento antes de aplicar más ajustes.");
                await stream.FlushAsync(token);
                stream.Flush(flushToDisk: true);
                async Task RewriteCompactAsync()
                {
                    stream.SetLength(0); stream.Position = 0;
                    await JsonSerializer.SerializeAsync(stream, entries, CompactOptions, token);
                }
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Validate(string tool, OptimizationValueChange value)
    {
        if (value is null || value.Previous is null || value.Intended is null)
            throw new InvalidDataException("Respaldo de mejoras incompleto.");
        var valid = tool switch
        {
            "game-mode" => value.Kind == "Registry" && value.Key == OptimizationTargets.GameBar && value.Name is "AutoGameModeEnabled" or "AllowAutoGameMode",
            "xbox-captures" => value.Kind == "Registry" &&
                (value.Key == OptimizationTargets.GameDvr && value.Name == "AppCaptureEnabled" ||
                 value.Key == OptimizationTargets.GameConfig && value.Name == "GameDVR_Enabled"),
            "animations" => value.Kind == "Animations" && value.Key == "" && value.Name == "",
            "visual-effects" => value.Kind == "VisualEffect" && value.Key == "" &&
                uint.TryParse(value.Name, out var action) && VisualEffects.Allows(action) &&
                value.Previous.Exists && value.Previous.Kind == Microsoft.Win32.RegistryValueKind.DWord && value.Previous.Number is 0 or 1 &&
                value.Intended.Same(RegistryValueState.Dword(0)),
            "power-mode" => value.Kind == "PowerMode" && value.Key == "AC" && value.Name == "" &&
                value.Previous.Exists && value.Previous.Kind == Microsoft.Win32.RegistryValueKind.String && Guid.TryParse(value.Previous.Text, out _) &&
                value.Intended.Same(RegistryValueState.String("ded574b5-45a0-4f42-8737-46345c09c238")),
            "fortnite-gpu" => value.Kind == "Registry" && value.Key == OptimizationTargets.GpuPreferences && FortniteGpuPreference.HasFortnitePathShape(value.Name),
            "application-gpu" => value.Kind == "Registry" && value.Key == OptimizationTargets.GpuPreferences && ApplicationGpuPreference.HasPathShape(value.Name) && FortniteGpuPreference.IsHighPerformance(value.Intended) == true,
            "power" => value.Kind == "Power" && value.Key == "" && value.Name == "" &&
                Guid.TryParse(value.Previous.Text, out _) && Guid.TryParse(value.Intended.Text, out _),
            "startup" => value.Kind == "Registry" && value.Key == OptimizationTargets.Run &&
                !string.IsNullOrWhiteSpace(value.Name) && value.Name.Length <= 16383 && !value.Name.Contains('\0'),
            _ => PerformanceTweaks.Allows(tool, value) || OptionalServices.Allows(tool, value) || WindowsUpdateDiagnostics.Allows(tool, value) || InputPreferences.Allows(tool, value)
        };
        if (!valid) throw new InvalidDataException("El respaldo contiene una acción o destino no permitido.");
        value.Previous.Validate();
        value.Intended.Validate();
        if (value.Kind == "Animations" && (!value.Previous.Exists || !value.Intended.Exists ||
            value.Previous.Kind != Microsoft.Win32.RegistryValueKind.DWord || value.Intended.Kind != Microsoft.Win32.RegistryValueKind.DWord ||
            value.Previous.Number is not (0 or 1) || value.Intended.Number is not (0 or 1)))
            throw new InvalidDataException("El respaldo de animaciones está dañado.");
    }
}
