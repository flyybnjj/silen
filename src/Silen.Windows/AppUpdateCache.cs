using System.Text.RegularExpressions;

namespace Silen.Windows;

internal static partial class AppUpdateCache
{
    private sealed record Entry(string Path, string Name, bool Directory, DateTime ModifiedUtc);
    internal static void Prune(string cacheRoot, string currentArchive)
    {
        BestEffort(() =>
        {
            cacheRoot = AppUpdatePaths.Validate(cacheRoot);
            currentArchive = AppUpdatePaths.Within(cacheRoot, currentArchive);
            if (!Directory.Exists(cacheRoot)) return;
            var entries = Directory.EnumerateFileSystemEntries(cacheRoot).Take(1024).Select(path => Inspect(cacheRoot, path)).OfType<Entry>().ToArray();
            var obsolete = entries.Where(entry => !entry.Directory && ArchiveName().IsMatch(entry.Name) && !entry.Path.Equals(currentArchive, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entry => entry.ModifiedUtc).ThenBy(entry => entry.Name, StringComparer.Ordinal).Skip(1);
            foreach (var entry in obsolete) DeleteFile(cacheRoot, entry.Path);
            var cutoff = DateTime.UtcNow.AddHours(-24);
            foreach (var entry in entries.Where(entry => !entry.Directory && PartialName().IsMatch(entry.Name) && entry.ModifiedUtc < cutoff)) DeleteFile(cacheRoot, entry.Path);
            PruneJobs(cacheRoot, cutoff);
        });
    }
    private static void PruneJobs(string cacheRoot, DateTime cutoff)
    {
        var jobs = AppUpdatePaths.Within(cacheRoot, Path.Combine(cacheRoot, "jobs"));
        if (!Directory.Exists(jobs)) return;
        var entries = Directory.EnumerateFileSystemEntries(jobs).Take(256).Select(path => Inspect(jobs, path)).OfType<Entry>()
            .Where(entry => entry.Directory && Guid.TryParseExact(entry.Name, "N", out _) && OwnedFiles(entry.Path) is not null)
            .OrderByDescending(entry => entry.ModifiedUtc).ThenBy(entry => entry.Name, StringComparer.Ordinal).Skip(2).ToArray();
        foreach (var entry in entries.Where(entry => entry.ModifiedUtc < cutoff))
            BestEffort(() =>
            {
                var path = AppUpdatePaths.Within(jobs, entry.Path);
                var files = OwnedFiles(path);
                if (files is null) return;
                foreach (var file in files) DeleteFile(path, file.Path);
                AppUpdatePaths.Validate(path);
                Directory.Delete(path, false);
            });
    }
    private static Entry[]? OwnedFiles(string path)
    {
        Entry[]? result = null;
        BestEffort(() =>
        {
            var children = Directory.EnumerateFileSystemEntries(AppUpdatePaths.Validate(path)).Take(3).ToArray();
            if (children.Length is 0 or > 2) return;
            var files = children.Select(child => Inspect(path, child)).OfType<Entry>().ToArray();
            if (files.Length != children.Length || files.Any(file => file.Directory || file.Name is not ("Silen.exe" or "job.bin"))) return;
            result = files;
        });
        return result;
    }
    private static Entry? Inspect(string root, string path)
    {
        Entry? entry = null;
        BestEffort(() =>
        {
            path = AppUpdatePaths.Within(root, path);
            var directory = (File.GetAttributes(path) & FileAttributes.Directory) != 0;
            entry = new(path, Path.GetFileName(path), directory, directory ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path));
        });
        return entry;
    }
    private static void DeleteFile(string root, string path) => BestEffort(() => File.Delete(AppUpdatePaths.Within(root, path)));
    private static void BestEffort(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException) { }
    }
    [GeneratedRegex(@"\A[0-9A-Fa-f]{64}\.zip\z", RegexOptions.CultureInvariant)]
    private static partial Regex ArchiveName();
    [GeneratedRegex(@"\A[0-9A-Fa-f]{32}\.partial\z", RegexOptions.CultureInvariant)]
    private static partial Regex PartialName();
}
