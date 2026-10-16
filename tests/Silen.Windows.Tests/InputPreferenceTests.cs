using Xunit;

namespace Silen.Windows.Tests;

public sealed class InputPreferenceTests
{
    [Fact]
    public async Task Gaming_keyboard_does_not_enable_click_sounds_from_the_old_accessibility_flags()
    {
        using var fixture = new OptimizationFixture();
        Assert.True((await fixture.Service().SetToggleAsync("input-filterkeys", true)).Success);
        Assert.Equal(0u, fixture.Platform.FilterKeys.Flags & 64u);
    }
    [Fact]
    public async Task Mouse_undo_restores_the_original_thresholds_and_acceleration()
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service(); var original = fixture.Platform.MouseMotion;
        Assert.True((await native.SetToggleAsync("input-mouse-linear", true)).Success);
        Assert.True((await native.UndoAsync(Assert.Single((await native.InspectAsync()).Changes).Id)).Success);
        Assert.Equal(original, fixture.Platform.MouseMotion);
    }
    [Fact]
    public async Task Filterkeys_profile_is_backed_before_SPI_and_restores_every_original_field()
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service();
        var original = fixture.Platform.FilterKeys;
        fixture.Platform.BeforeInputWrite = () => Assert.Contains("Pending", File.ReadAllText(Path.Combine(fixture.Journal, "optimizations.json")));
        Assert.True((await native.SetToggleAsync("input-filterkeys", true)).Success);
        Assert.Equal(new FilterKeysPreference(59, 0, 130, 10, 0), fixture.Platform.FilterKeys);
        fixture.Platform.BeforeInputWrite = null;
        Assert.True((await native.UndoAsync(Assert.Single((await native.InspectAsync()).Changes).Id)).Success);
        Assert.Equal(original, fixture.Platform.FilterKeys);
    }
    [Fact]
    public async Task Linear_mouse_restores_thresholds_and_preserves_external_changes()
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service();
        Assert.True((await native.SetToggleAsync("input-mouse-linear", true)).Success);
        Assert.Equal(new MouseMotionPreference(0, 0, 0), fixture.Platform.MouseMotion);
        var backup = Assert.Single((await native.InspectAsync()).Changes);
        fixture.Platform.MouseMotion = new(3, 7, 1);
        Assert.False((await native.UndoAsync(backup.Id)).Success);
        Assert.Equal(new MouseMotionPreference(3, 7, 1), fixture.Platform.MouseMotion);
    }
    [Fact]
    public async Task Existing_input_profile_does_not_create_a_fictitious_backup()
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service();
        fixture.Platform.FilterKeys = new(27, 0, 130, 10, 0); fixture.Platform.MouseMotion = new(0, 0, 0);
        Assert.True((await native.SetToggleAsync("input-filterkeys", true)).Success);
        Assert.True((await native.SetToggleAsync("input-mouse-linear", true)).Success);
        Assert.Empty((await native.InspectAsync()).Changes);
    }
}
