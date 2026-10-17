using System.Text.Json.Nodes;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class LegacyJournalConservationTests
{
    [Fact]
    public async Task Successive_legacy_sources_are_all_preserved_before_user_history_is_rewritten()
    {
        using var fixture = new OptimizationFixture();
        var legacy = fixture.Service();
        Assert.True((await legacy.SetToggleAsync("delivery-optimization", true)).Success);
        var path = Path.Combine(fixture.Journal, "optimizations.json");
        var first = await File.ReadAllBytesAsync(path);
        await new OptimizationJournal(fixture.Journal).ReadAsync(default);
        var document = JsonNode.Parse(first)!.AsArray();
        document[0]!["Id"] = Guid.NewGuid().ToString("N");
        document[0]!["Title"] = "Segundo origen legado";
        var second = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString());
        await File.WriteAllBytesAsync(path, second);
        var scoped = new NativeOptimizationService(fixture.Platform, fixture.Journal, fixture.Temp,
            () => DateTimeOffset.UtcNow, authority: OptimizationAuthority.User);
        Assert.True((await scoped.SetToggleAsync("game-mode", true)).Success);
        var copies = Directory.GetFiles(fixture.Journal, "optimizations.legacy-untrusted*.json")
            .Select(File.ReadAllBytes).ToArray();
        Assert.Contains(copies, bytes => bytes.SequenceEqual(first));
        Assert.Contains(copies, bytes => bytes.SequenceEqual(second));
        Assert.Equal(first, await File.ReadAllBytesAsync(Path.Combine(fixture.Journal, "optimizations.legacy-untrusted.json")));
        Assert.Single(await new OptimizationJournal(fixture.Journal).ReadAsync(default));
    }
}
