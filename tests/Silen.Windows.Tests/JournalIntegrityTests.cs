using System.Text.Json.Nodes;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class JournalIntegrityTests
{
    [Theory]
    [InlineData("Id")]
    [InlineData("State")]
    [InlineData("Previous.Exists")]
    [InlineData("Previous.Number")]
    public async Task Missing_original_state_is_rejected_without_writes_or_repairing_the_file(string missing)
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.GameBar, "AutoGameModeEnabled", RegistryValueState.Dword(37));
        var service = fixture.Service();
        Assert.True((await service.SetToggleAsync("game-mode", true)).Success);
        var path = Path.Combine(fixture.Journal, "optimizations.json");
        var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsArray();
        var id = document[0]!["Id"]!.GetValue<string>();
        if (missing.StartsWith("Previous."))
            document[0]!["Values"]![0]!["Previous"]!.AsObject().Remove(missing["Previous.".Length..]);
        else document[0]!.AsObject().Remove(missing);
        var corrupt = document.ToJsonString();
        await File.WriteAllTextAsync(path, corrupt);
        var before = fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled");
        await Assert.ThrowsAsync<InvalidDataException>(() => new OptimizationJournal(fixture.Journal).ReadAsync(default));
        var result = await service.UndoAsync(id);
        Assert.False(result.Success);
        Assert.True(before.Same(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled")));
        Assert.Equal(corrupt, await File.ReadAllTextAsync(path));
    }
}
