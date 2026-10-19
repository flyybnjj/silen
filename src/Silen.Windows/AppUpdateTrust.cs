using System.Text.Json;
using System.Text.RegularExpressions;

namespace Silen.Windows;

internal static partial class AppUpdateTrust
{
    internal const string AssetName = "Silen-win-x64.zip";
    internal const string ApiRoot = "https://api.github.com/repos/flyybnjj/silen/releases/";
    internal const long MaxArchiveBytes = 512L * 1024 * 1024;
    internal static AppRelease Parse(ReadOnlyMemory<byte> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) throw Invalid();
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var version = ParseVersion(tag);
            var assets = root.GetProperty("assets");
            if (assets.GetArrayLength() > 100) throw Invalid();
            var matches = assets.EnumerateArray().Where(a => a.GetProperty("name").GetString() == AssetName).ToArray();
            if (matches.Length != 1) throw Invalid();
            var asset = matches[0];
            var size = asset.GetProperty("size").GetInt64();
            var digest = asset.GetProperty("digest").GetString() ?? "";
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (size <= 0 || size > MaxArchiveBytes || !DigestPattern().IsMatch(digest) ||
                url != $"https://github.com/flyybnjj/silen/releases/download/{tag}/{AssetName}") throw Invalid();
            return new(version, tag, url, size, digest[7..].ToUpperInvariant());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException("GitHub devolvió metadatos de actualización inválidos.", ex); }
    }
    internal static Version ParseVersion(string tag)
    {
        if (tag.Length > 50 || !VersionPattern().IsMatch(tag) || !Version.TryParse(tag.TrimStart('v'), out var version)) throw Invalid();
        return Normalize(version);
    }
    internal static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    internal static void Validate(AppRelease release)
    {
        if (ParseVersion(release.Tag) != Normalize(release.Version) || release.Size <= 0 || release.Size > MaxArchiveBytes ||
            !DigestPattern().IsMatch("sha256:" + release.Sha256) ||
            release.AssetUrl != $"https://github.com/flyybnjj/silen/releases/download/{release.Tag}/{AssetName}") throw Invalid();
    }
    private static InvalidDataException Invalid() => new("La release no es estable o no tiene un archivo oficial con SHA-256 válido.");
    [GeneratedRegex(@"\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*)){0,2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
    [GeneratedRegex(@"\Asha256:[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();
}
