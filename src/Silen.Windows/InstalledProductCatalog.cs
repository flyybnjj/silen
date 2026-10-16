using Microsoft.Win32;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Silen.Windows;

public sealed record InstalledProduct(string Id, string Name, string Publisher, string? InstallLocation, string? Executable, string Source);
public interface IInstalledProductCatalog
{
    Task<IReadOnlyList<InstalledProduct>> FindAsync(CancellationToken token = default);
    Task<IReadOnlyList<string>> LocateExecutablesAsync(InstalledProduct product, CancellationToken token = default);
}
public sealed class InstalledProductCatalog : IInstalledProductCatalog
{
    public Task<IReadOnlyList<InstalledProduct>> FindAsync(CancellationToken token = default) => Task.Run<IReadOnlyList<InstalledProduct>>(() =>
    {
        var products = new List<InstalledProduct>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var name in uninstall.GetSubKeyNames().Take(3000))
                {
                    token.ThrowIfCancellationRequested();
                    using var item = uninstall.OpenSubKey(name);
                    if (item?.GetValue("DisplayName") is not string title || string.IsNullOrWhiteSpace(title) || item.GetValue("SystemComponent") is int hidden && hidden == 1) continue;
                    var folder = CanonicalDirectory(item.GetValue("InstallLocation") as string);
                    var icon = IconExecutable(item.GetValue("DisplayIcon") as string);
                    products.Add(Product(title, item.GetValue("Publisher") as string ?? "", folder, icon, "Registro de aplicaciones"));
                }
            }
            catch (Exception ex) when (Expected(ex)) { }
        }
        var epic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        foreach (var file in SafeFiles(epic, "*.item", 1000))
        {
            token.ThrowIfCancellationRequested();
            try { if (ReadSmallFile(file) is { } json && ParseEpic(json) is { } product) products.Add(product); }
            catch (Exception ex) when (Expected(ex)) { }
        }
        var steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view); using var key = root.OpenSubKey(@"SOFTWARE\Valve\Steam");
                if (CanonicalDirectory((key?.GetValue("SteamPath") ?? key?.GetValue("InstallPath")) as string) is { } path) steamRoots.Add(path);
            }
            catch (Exception ex) when (Expected(ex)) { }
        }
        var libraries = new HashSet<string>(steamRoots, StringComparer.OrdinalIgnoreCase);
        foreach (var steam in steamRoots)
        {
            var vdf = ReadSmallFile(Path.Combine(steam, "steamapps", "libraryfolders.vdf"));
            if (vdf is null) continue;
            foreach (Match match in Regex.Matches(vdf, "\"path\"\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                if (CanonicalDirectory(match.Groups[1].Value.Replace(@"\\", @"\")) is { } path) libraries.Add(path);
        }
        foreach (var library in libraries.Take(32))
        foreach (var file in SafeFiles(Path.Combine(library, "steamapps"), "appmanifest_*.acf", 1000))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (ReadSmallFile(file) is not { } acf) continue;
                var title = AcfValue(acf, "name"); var directory = AcfValue(acf, "installdir");
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(directory)) continue;
                var root = Path.Combine(library, "steamapps", "common");
                var folder = Path.GetFullPath(Path.Combine(root, directory));
                if (Inside(folder, root)) products.Add(Product(title, "Steam", folder, null, "Biblioteca Steam"));
            }
            catch (Exception ex) when (Expected(ex)) { }
        }
        return products.GroupBy(product => product.Name + "|" + (product.InstallLocation ?? product.Executable ?? ""), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(product => product.Source != "Registro de aplicaciones").First()).OrderBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase).Take(3000).ToArray();
    }, token);

    public Task<IReadOnlyList<string>> LocateExecutablesAsync(InstalledProduct product, CancellationToken token = default) => Task.Run<IReadOnlyList<string>>(() =>
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (product.Executable is { } executable && SafeExecutable(executable)) result.Add(executable);
        var root = CanonicalDirectory(product.InstallLocation);
        if (root is null || !Directory.Exists(root) || Path.GetPathRoot(root)?.TrimEnd('\\') == root.TrimEnd('\\')) return result.ToArray();
        var pending = new Stack<(string Path, int Depth)>(); pending.Push((root, 0)); var inspected = 0;
        while (pending.TryPop(out var current) && inspected < 3000 && result.Count < 100)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if ((File.GetAttributes(current.Path) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (var entry in Directory.EnumerateFileSystemEntries(current.Path))
                {
                    token.ThrowIfCancellationRequested(); if (++inspected > 3000) break;
                    var attributes = File.GetAttributes(entry); if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0) { if (current.Depth < 4) pending.Push((entry, current.Depth + 1)); }
                    else if (SafeExecutable(entry)) result.Add(Path.GetFullPath(entry));
                }
            }
            catch (Exception ex) when (Expected(ex)) { }
        }
        return result.OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase).Take(100).ToArray();
    }, token);

    internal static InstalledProduct? ParseEpic(string json)
    {
        using var doc = JsonDocument.Parse(json); var data = doc.RootElement;
        if (!data.TryGetProperty("DisplayName", out var name) || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString())) return null;
        var folder = data.TryGetProperty("InstallLocation", out var location) ? CanonicalDirectory(location.GetString()) : null;
        string? executable = null;
        if (folder is not null && data.TryGetProperty("LaunchExecutable", out var launch) && launch.GetString() is { } relative)
        {
            var path = Path.GetFullPath(Path.Combine(folder, relative.Replace('/', '\\')));
            if (Inside(path, folder) && Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)) executable = path;
        }
        return Product(name.GetString()!, "Epic Games", folder, executable, "Manifiesto Epic");
    }
    private static InstalledProduct Product(string name, string publisher, string? folder, string? executable, string source) =>
        new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name + "|" + folder + "|" + source))), name, publisher, folder, executable, source);
    private static string? CanonicalDirectory(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path.Trim()) && !path.Trim().StartsWith(@"\\") ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim())) : null;
    private static bool Inside(string path, string folder) => path.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static string? IconExecutable(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return null;
        var match = Regex.Match(icon, "^\\s*\"?(.*?\\.exe)\"?(?:,[-0-9]+)?\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return match.Success ? CanonicalDirectory(Environment.ExpandEnvironmentVariables(match.Groups[1].Value)) : null;
    }
    private static bool SafeExecutable(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) || new[] { "unins", "uninstall", "setup", "crash", "report", "anticheat", "easyanticheat", "beservice", "vcredist", "update" }.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase))) return false;
        try { return File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0; } catch (Exception ex) when (Expected(ex)) { return false; }
    }
    private static IEnumerable<string> SafeFiles(string path, string pattern, int maximum)
    { try { return Directory.Exists(path) ? Directory.EnumerateFiles(path, pattern).Take(maximum).ToArray() : []; } catch (Exception ex) when (Expected(ex)) { return []; } }
    private static string? ReadSmallFile(string path)
    { try { return File.Exists(path) && new FileInfo(path).Length <= 262144 ? File.ReadAllText(path) : null; } catch (Exception ex) when (Expected(ex)) { return null; } }
    private static string? AcfValue(string text, string key)
    { var match = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); return match.Success ? match.Groups[1].Value : null; }
    private static bool Expected(Exception exception) => exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or JsonException or InvalidOperationException or RegexMatchTimeoutException;
}
