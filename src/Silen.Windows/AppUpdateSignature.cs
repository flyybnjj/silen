using System.Security.Cryptography;
using System.Text.Json;

namespace Silen.Windows;

internal static class AppUpdateSignature
{
    internal const string ManifestName = "Silen-release.json";
    internal const int MaxManifestBytes = 32768;
    internal static string EmbeddedPublicKey()
    {
        using var stream = typeof(AppUpdateSignature).Assembly.GetManifestResourceStream("Silen.UpdateSigningPublicKey.pem");
        if (stream is null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static string ManifestUrl(ReadOnlyMemory<byte> metadata, AppRelease release)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadata);
            var assets = doc.RootElement.GetProperty("assets").EnumerateArray()
                .Where(a => a.GetProperty("name").GetString() == ManifestName).ToArray();
            var expected = $"https://github.com/flyybnjj/flyyoptimizer/releases/download/{release.Tag}/{ManifestName}";
            if (assets.Length != 1 || assets[0].GetProperty("browser_download_url").GetString() != expected ||
                assets[0].GetProperty("size").GetInt64() is <= 0 or > MaxManifestBytes) throw Invalid();
            return expected;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw Invalid(ex); }
    }

    internal static void Verify(ReadOnlyMemory<byte> manifest, AppRelease release, string publicKey)
    {
        if (string.IsNullOrWhiteSpace(publicKey))
            throw new InvalidDataException("Esta versión no tiene una clave de editor configurada. Las actualizaciones automáticas están bloqueadas hasta instalar una versión con clave confiable.");
        try
        {
            if (manifest.Length is 0 or > MaxManifestBytes) throw Invalid();
            using var envelope = JsonDocument.Parse(manifest, new JsonDocumentOptions { MaxDepth = 4 });
            var root = envelope.RootElement;
            if (root.EnumerateObject().Count() != 2) throw Invalid();
            var payload = Convert.FromBase64String(root.GetProperty("payload").GetString() ?? "");
            var signature = Convert.FromBase64String(root.GetProperty("signature").GetString() ?? "");
            if (payload.Length is 0 or > 8192 || signature.Length != 64) throw Invalid();
            using var key = ECDsa.Create();
            key.ImportFromPem(publicKey);
            if (key.KeySize != 256 || !key.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) throw Invalid();
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 4 });
            var value = document.RootElement;
            if (value.EnumerateObject().Count() != 6 || value.GetProperty("schemaVersion").GetInt32() != 1 ||
                value.GetProperty("repository").GetString() != "flyybnjj/flyyoptimizer" ||
                value.GetProperty("tag").GetString() != release.Tag || value.GetProperty("asset").GetString() != AppUpdateTrust.AssetName ||
                value.GetProperty("size").GetInt64() != release.Size || value.GetProperty("sha256").GetString() != release.Sha256.ToUpperInvariant()) throw Invalid();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or CryptographicException or ArgumentException or OverflowException)
        { throw Invalid(ex); }
    }

    private static InvalidDataException Invalid(Exception? inner = null) =>
        new("La publicación no tiene una firma válida del editor. Silen bloqueó la actualización; solicita una release firmada.", inner);
}
