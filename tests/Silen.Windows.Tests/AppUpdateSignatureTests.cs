using System.Security.Cryptography;
using System.Text.Json;
using Silen.Windows;
using Xunit;

namespace Silen.Windows.Tests;

internal static class TestSigningKey
{
    private static readonly ECDsa Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    internal static string PublicKey => Key.ExportSubjectPublicKeyInfoPem();
    internal static byte[] Manifest(AppRelease release, ECDsa? signingKey = null)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, repository = "flyybnjj/silen", tag = release.Tag, asset = "Silen-win-x64.zip", size = release.Size, sha256 = release.Sha256.ToUpperInvariant() });
        byte[] signature;
        lock (Key) signature = (signingKey ?? Key).SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return JsonSerializer.SerializeToUtf8Bytes(new { payload = Convert.ToBase64String(payload), signature = Convert.ToBase64String(signature) });
    }
}

public sealed class AppUpdateSignatureTests
{
    private static readonly AppRelease Release = new(new Version(1, 2, 0, 0), "v1.2.0", "https://github.com/flyybnjj/silen/releases/download/v1.2.0/Silen-win-x64.zip", 42, new string('A', 64));
    [Fact]
    public void Valid_publisher_signature_accepts_exact_release() => AppUpdateSignature.Verify(TestSigningKey.Manifest(Release), Release, TestSigningKey.PublicKey);
    [Fact]
    public void Foreign_signing_key_is_rejected()
    {
        using var attacker = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Throws<InvalidDataException>(() => AppUpdateSignature.Verify(TestSigningKey.Manifest(Release, attacker), Release, TestSigningKey.PublicKey));
    }
    [Theory]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("tag")]
    public void Signed_manifest_cannot_be_reused_for_modified_metadata(string field)
    {
        var altered = field switch { "hash" => Release with { Sha256 = new string('B', 64) }, "size" => Release with { Size = 43 }, _ => Release with { Tag = "v1.3.0" } };
        Assert.Throws<InvalidDataException>(() => AppUpdateSignature.Verify(TestSigningKey.Manifest(Release), altered, TestSigningKey.PublicKey));
    }
    [Theory]
    [InlineData("")]
    [InlineData("invalid key")]
    public void Missing_or_invalid_trust_anchor_fails_closed(string key) => Assert.Throws<InvalidDataException>(() => AppUpdateSignature.Verify(TestSigningKey.Manifest(Release), Release, key));
    [Fact]
    public void Altered_signed_payload_is_rejected()
    {
        var bytes = TestSigningKey.Manifest(Release);
        using var doc = JsonDocument.Parse(bytes);
        var payload = Convert.FromBase64String(doc.RootElement.GetProperty("payload").GetString()!);
        payload[^2] ^= 1;
        var altered = JsonSerializer.SerializeToUtf8Bytes(new { payload = Convert.ToBase64String(payload), signature = doc.RootElement.GetProperty("signature").GetString() });
        Assert.Throws<InvalidDataException>(() => AppUpdateSignature.Verify(altered, Release, TestSigningKey.PublicKey));
    }
}
