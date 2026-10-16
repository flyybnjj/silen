using System.Security.Cryptography;
using System.Text.Json;

namespace Silen.Windows;

internal sealed class FortniteGameChange
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SettingsPath { get; set; } = "";
    public string PreviousHash { get; set; } = "";
    public string IntendedHash { get; set; } = "";
    public string FileIdentity { get; set; } = "";
    public DateTimeOffset AppliedAt { get; set; }
    public string State { get; set; } = "Pending";
    public int ModifiedKeys { get; set; }
}

internal sealed class FortniteGameJournal(string directory, string settingsPath)
{
    private const int MaximumJournalBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string directory = FortniteSafeFiles.LocalPath(directory);
    private readonly string settingsPath = FortniteSafeFiles.LocalPath(settingsPath);

    internal async Task<IDisposable> LeaseAsync(CancellationToken token)
    {
        var folders = FortniteSafeFiles.LockDirectory(directory, create: true);
        try
        {
            var path = Path.Combine(directory, "fortnite.lock");
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (!File.Exists(path))
                    {
                        try { FortniteSafeFiles.WriteDurableNew(path, []); }
                        catch (IOException) when (File.Exists(path)) { }
                    }
                    return new JournalLease(FortniteSafeFiles.Open(path, false, maximumBytes: 0), folders);
                }
                catch (IOException ex) when (ex.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021))
                {
                    if (DateTime.UtcNow >= deadline) throw new IOException("Otra instancia está revisando Fortnite. Vuelve a intentarlo.", ex);
                    await Task.Delay(25, token);
                }
            }
        }
        catch { folders.Dispose(); throw; }
    }

    internal List<FortniteGameChange> Read()
    {
        var path = Path.Combine(directory, "fortnite.json");
        if (!File.Exists(path)) return [];
        using var file = FortniteSafeFiles.Open(path, false, maximumBytes: MaximumJournalBytes);
        List<FortniteGameChange>? entries;
        try { entries = JsonSerializer.Deserialize<List<FortniteGameChange>>(file.ReadBytes(), Options); }
        catch (JsonException ex) { throw new InvalidDataException("El historial de Fortnite está dañado y se conservó.", ex); }
        if (entries is null || entries.Count > 256) throw new InvalidDataException("El historial de Fortnite es incompatible.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
            if (entry is null || !Guid.TryParseExact(entry.Id, "N", out _) || !ids.Add(entry.Id) ||
                !string.Equals(entry.SettingsPath, settingsPath, StringComparison.OrdinalIgnoreCase) ||
                !IsHash(entry.PreviousHash) || !IsHash(entry.IntendedHash) || entry.PreviousHash == entry.IntendedHash ||
                entry.FileIdentity is not { Length: 24 } || !entry.FileIdentity.All(Uri.IsHexDigit) ||
                entry.ModifiedKeys is < 1 or > 5000 || entry.State is not ("Pending" or "Completed" or "Partial" or "UndoPartial" or "UndoPending" or "Undone" or "NotApplied" or "Conflict"))
                throw new InvalidDataException("El historial contiene un respaldo o destino no permitido.");
        return entries;
    }

    internal void Write(List<FortniteGameChange> entries)
    {
        if (entries.Count > 256) throw new IOException("El historial de Fortnite está lleno. Se conservaron los respaldos.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entries, Options);
        if (bytes.Length > MaximumJournalBytes) throw new IOException("El historial de Fortnite supera el tamaño permitido.");
        var path = Path.Combine(directory, "fortnite.json");
        if (File.Exists(path))
        { using var existing = FortniteSafeFiles.Open(path, true, maximumBytes: MaximumJournalBytes); }
        {
            var temporary = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                FortniteSafeFiles.WriteDurableNew(temporary, bytes);
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    internal void Backup(FortniteGameChange entry, byte[] original, byte[] intended)
    {
        FortniteSafeFiles.WriteDurableNew(BackupPath(entry), original);
        FortniteSafeFiles.WriteDurableNew(BackupPath(entry, intended: true), intended);
    }

    internal byte[] ReadBackup(FortniteGameChange entry)
        => ReadBackup(entry, false);

    internal byte[] ReadBackup(FortniteGameChange entry, bool intended)
    {
        using var file = FortniteSafeFiles.Open(BackupPath(entry, intended), false);
        var bytes = file.ReadBytes();
        if (!Hash(bytes).Equals(intended ? entry.IntendedHash : entry.PreviousHash, StringComparison.Ordinal))
            throw new InvalidDataException("El respaldo de Fortnite cambió o está dañado. No se restauró.");
        return bytes;
    }

    private string BackupPath(FortniteGameChange entry, bool intended = false)
    {
        if (!Guid.TryParseExact(entry.Id, "N", out _)) throw new InvalidDataException("El identificador del respaldo no es válido.");
        return Path.Combine(directory, entry.Id + (intended ? ".intended.bin" : ".original.bin"));
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static bool IsHash(string value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private sealed class JournalLease(IDisposable file, IDisposable folders) : IDisposable
    {
        public void Dispose() { file.Dispose(); folders.Dispose(); }
    }
}
