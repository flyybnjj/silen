using System.Text;
using Silen.Core;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class BloomStorageTests
{
    [Fact]
    public void DPAPI_round_trip_never_writes_plaintext_and_corruption_is_not_a_missing_key()
    {
        using var fixture = new OptimizationFixture();
        var path = Path.Combine(fixture.Temp, "bloom.dat");
        var store = new BloomKeyStore(path);
        Assert.Null(store.Read());
        store.Save("fake-test-key-never-send");
        Assert.Equal("fake-test-key-never-send", store.Read());
        Assert.DoesNotContain("fake-test-key-never-send", Encoding.UTF8.GetString(File.ReadAllBytes(path)));
        File.WriteAllBytes(path, [1, 2, 3]);
        var error = Assert.Throws<BloomException>(() => store.Read());
        Assert.DoesNotContain("fake-test", error.ToString());
        store.Delete(); Assert.Null(store.Read());
    }
    [Fact]
    public void Key_path_defaults_to_roaming_Silen_without_reusing_diagnostic_history()
    {
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Silen", "bloom.dat"), BloomKeyStore.DefaultPath);
    }
    [Fact]
    public void Separate_provider_credentials_keep_legacy_Gemini_path_compatible()
    {
        using var fixture = new OptimizationFixture();
        var legacy = new BloomKeyStore(Path.Combine(fixture.Temp, "bloom.dat"));
        var openai = new BloomKeyStore(Path.Combine(fixture.Temp, "bloom-openai.dat"));
        var anthropic = new BloomKeyStore(Path.Combine(fixture.Temp, "bloom-anthropic.dat"));
        legacy.Save("fake-gemini"); openai.Save("fake-openai"); anthropic.Save("fake-anthropic");
        openai.Delete(); Assert.Null(openai.Read()); Assert.Equal("fake-gemini", legacy.Read()); Assert.Equal("fake-anthropic", anthropic.Read());
    }
}
