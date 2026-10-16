using System.IO.Compression;

namespace Silen.Windows;

internal static class AppUpdateArchive
{
    private const long MaxExpanded = 1024L * 1024 * 1024;
    internal static async Task ExtractAsync(string archivePath, string stage, CancellationToken token)
    {
        AppUpdatePaths.Validate(archivePath);
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count is 0 or > 4096) throw Invalid();
        var seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var accepted = new List<(ZipArchiveEntry Entry, string Relative)>();
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = entry.FullName;
            if (name.Length > 240 || name.Contains('\\') || name.Contains(':') || !name.StartsWith("Silen-win-x64/", StringComparison.Ordinal)) throw Invalid();
            var parts = name.TrimEnd('/').Split('/');
            if (parts.Length > 18 || parts.Any(UnsafeSegment)) throw Invalid();
            var directory = name.EndsWith('/');
            var key = name.TrimEnd('/');
            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if ((entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0 || unixType is not (0 or 0x8000 or 0x4000) ||
                (unixType == 0x4000 && !directory) || !seen.TryAdd(key, directory)) throw Invalid();
            if (parts.Length == 1) { if (!directory) throw Invalid(); continue; }
            if (parts[1] != "Archivos" && !(parts.Length == 2 && parts[1] == "Silen.exe" && !directory)) throw Invalid();
            if (parts.Length == 2 && parts[1] == "Archivos" && !directory) throw Invalid();
            if (entry.Length > AppUpdateTrust.MaxArchiveBytes || entry.Length < 0 || directory && entry.Length != 0 ||
                entry.Length > 1024 * 1024 && entry.Length / Math.Max(1, entry.CompressedLength) > 200) throw Invalid();
            total = checked(total + entry.Length);
            if (total > MaxExpanded) throw Invalid();
            accepted.Add((entry, string.Join(Path.DirectorySeparatorChar, parts.Skip(1))));
        }
        foreach (var key in seen.Keys)
        {
            var parent = key;
            while (parent.LastIndexOf('/') is var index && index >= 0)
            {
                parent = parent[..index];
                if (seen.TryGetValue(parent, out var directory) && !directory) throw Invalid();
            }
        }
        if (!seen.ContainsKey("Silen-win-x64/Silen.exe") || !accepted.Any(x => x.Relative == "Archivos" || x.Relative.StartsWith("Archivos" + Path.DirectorySeparatorChar, StringComparison.Ordinal))) throw Invalid();
        foreach (var (entry, relative) in accepted)
        {
            token.ThrowIfCancellationRequested();
            var destination = AppUpdatePaths.Within(stage, Path.Combine(stage, relative));
            if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(destination); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var source = entry.Open();
            await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
            var block = new byte[65536];
            long written = 0;
            int count;
            while ((count = await source.ReadAsync(block, token).ConfigureAwait(false)) != 0)
            {
                written += count;
                if (written > entry.Length) throw Invalid();
                await target.WriteAsync(block.AsMemory(0, count), token).ConfigureAwait(false);
            }
            if (written != entry.Length) throw Invalid();
        }
        using var executable = File.OpenRead(Path.Combine(stage, "Silen.exe"));
        if (executable.ReadByte() != 'M' || executable.ReadByte() != 'Z') throw new InvalidDataException("El archivo principal no tiene cabecera de ejecutable.");
    }
    private static bool UnsafeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value.EndsWith('.') || value.EndsWith(' ') || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return true;
        var stem = value.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".Contains(stem[3]);
    }
    private static InvalidDataException Invalid() => new("El ZIP contiene rutas, enlaces, duplicados o tamaños no permitidos para la aplicación portable.");
}
