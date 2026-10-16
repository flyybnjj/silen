using Xunit;

namespace Silen.Windows.Tests;

public sealed class JournalCapacityTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Legacy_journal_at_the_exact_read_limit_can_still_be_undone(bool indented, bool interrupt)
    {
        using var fixture = new OptimizationFixture();
        await fixture.Service().SetToggleAsync("game-mode", true);
        var journal = new OptimizationJournal(fixture.Journal, OptimizationAuthority.TestOnly);
        var entries = await journal.ReadAsync(default);
        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = indented,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
        var initial = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(entries, options);
        entries[0].Title = new string('x', entries[0].Title.Length + OptimizationJournal.MaximumBytes - initial.Length);
        var legacy = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(entries, options);
        Assert.Equal(OptimizationJournal.MaximumBytes, legacy.Length);
        await File.WriteAllBytesAsync(Path.Combine(fixture.Journal, "optimizations.json"), legacy);
        if (interrupt)
        {
            fixture.Platform.FailWriteNumber = 4; // Dos escrituras originales; interrumpir la segunda restauración.
            Assert.False((await fixture.Service().UndoAsync(entries[0].Id)).Success);
            Assert.Equal("Pending", Assert.Single(await journal.ReadAsync(default)).State);
            fixture.Platform.FailWriteNumber = null;
        }
        var restored = await fixture.Service().UndoAsync(entries[0].Id);
        Assert.True(restored.Success, restored.Message);
        var after = Assert.Single(await journal.ReadAsync(default));
        Assert.Equal("Undone", after.State);
        Assert.Equal(entries[0].Title, after.Title);
        Assert.Equal(entries[0].Values, after.Values);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Exists);
    }
    [Fact]
    public async Task Oversized_write_cannot_replace_a_readable_backup()
    {
        using var fixture = new OptimizationFixture();
        await fixture.Service().SetToggleAsync("game-mode", true);
        var path = Path.Combine(fixture.Journal, "optimizations.json");
        var previous = await File.ReadAllBytesAsync(path);
        var journal = new OptimizationJournal(fixture.Journal, OptimizationAuthority.TestOnly);
        var entries = await journal.ReadAsync(default);
        entries.Add(new() { ToolId = "game-mode", Title = new string('x', 16 * 1024 * 1024),
            Values = [new("Registry", OptimizationTargets.GameBar, "AllowAutoGameMode", RegistryValueState.Absent, RegistryValueState.Dword(1))] });
        await Assert.ThrowsAsync<InvalidDataException>(() => journal.WriteAsync(entries, default));
        Assert.Equal(previous, await File.ReadAllBytesAsync(path));
        Assert.Single(await journal.ReadAsync(default));
        Assert.True((await fixture.Service().UndoAsync(entries[0].Id)).Success);
        Assert.Empty(Directory.GetFiles(fixture.Journal, "*.tmp"));
    }

    [Fact]
    public async Task Full_journal_blocks_a_new_system_write_but_keeps_the_existing_undo()
    {
        using var fixture = new OptimizationFixture();
        await fixture.Service().SetToggleAsync("game-mode", true);
        var journal = new OptimizationJournal(fixture.Journal, OptimizationAuthority.TestOnly);
        var entries = await journal.ReadAsync(default);
        var path = Path.Combine(fixture.Journal, "optimizations.json");
        entries[0].Title = new string('x', OptimizationJournal.MaximumBytes - (int)new FileInfo(path).Length - 128);
        await journal.WriteAsync(entries, default);
        var before = await File.ReadAllBytesAsync(path);
        var writes = 0;
        fixture.Platform.BeforeWrite = () => writes++;
        var blocked = await fixture.Service().SetToggleAsync("xbox-captures", true);
        Assert.False(blocked.Success);
        Assert.False(blocked.MayHaveChanged);
        Assert.Equal(0, writes);
        Assert.Contains("capacidad", blocked.Message);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.True((await fixture.Service().UndoAsync(entries[0].Id)).Success);
    }
}
