using Silen.Windows;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class PerformanceControlsTests
{
    [Fact]
    public async Task Best_performance_mode_restores_original_and_preserves_external_change()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Overlay = Guid.Empty;
        var service = fixture.Service();
        Assert.True((await service.SetBestPerformanceModeAsync(() => true)).Success);
        Assert.Equal(Guid.Parse("ded574b5-45a0-4f42-8737-46345c09c238"), fixture.Platform.Overlay);
        var change = Assert.Single((await service.InspectAsync()).Changes);
        fixture.Platform.Overlay = Guid.Parse("961cc777-2547-4f9d-8174-7d86181b8a7a");
        Assert.False((await service.UndoAsync(change.Id)).Success);
        fixture.Platform.Overlay = Guid.Parse("ded574b5-45a0-4f42-8737-46345c09c238");
        Assert.True((await service.UndoAsync(change.Id)).Success);
        Assert.Equal(Guid.Empty, fixture.Platform.Overlay);
    }

    [Fact]
    public async Task Battery_or_unavailable_power_mode_makes_no_changes()
    {
        using var fixture = new OptimizationFixture();
        Assert.False((await fixture.Service().SetBestPerformanceModeAsync(() => false)).Success);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
        fixture.Platform.Overlay = null;
        Assert.Null(fixture.Service().ReadPowerMode());
        Assert.False((await fixture.Service().SetBestPerformanceModeAsync(() => true)).Success);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Power_source_change_before_write_preserves_mode_and_recovery()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Overlay = Guid.Empty;
        var checks = 0;
        var service = fixture.Service();
        Assert.False((await service.SetBestPerformanceModeAsync(() => ++checks == 1)).Success);
        Assert.Equal(Guid.Empty, fixture.Platform.Overlay);
        var entry = Assert.Single((await service.InspectAsync()).Changes);
        Assert.True((await service.UndoAsync(entry.Id)).Success);
    }

    [Fact]
    public async Task Reduced_visual_effects_are_applied_live_and_restored_individually()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Effects[0x1004] = false;
        var service = fixture.Service();
        Assert.True((await service.ReduceVisualEffectsAsync()).Success);
        Assert.All(fixture.Platform.Effects.Values, value => Assert.False(value));
        var change = Assert.Single((await service.InspectAsync()).Changes);
        Assert.True((await service.UndoAsync(change.Id)).Success);
        Assert.True(fixture.Platform.Effects[0x1002]);
        Assert.False(fixture.Platform.Effects[0x1004]);
        Assert.True(fixture.Platform.Effects[0x1016]);
    }

    [Theory]
    [InlineData("gamebar-background", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AudioCaptureEnabled")]
    [InlineData("desktop-composition", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency")]
    public async Task Recording_and_composition_preserve_original_and_absent_values(string group, string key, string name)
    {
        using var fixture = new OptimizationFixture();
        var service = fixture.Service();
        fixture.Platform.WriteValue(key, name, RegistryValueState.Dword(1));
        Assert.True((await service.ApplyPerformanceTweakAsync(group)).Success);
        Assert.Equal(0, fixture.Platform.ReadValue(key, name).Number);
        var change = Assert.Single((await service.InspectAsync()).Changes);
        Assert.True((await service.UndoAsync(change.Id)).Success);
        Assert.Equal(1, fixture.Platform.ReadValue(key, name).Number);
        Assert.False(fixture.Platform.ReadValue(group == "gamebar-background" ? @"Software\Microsoft\GameBar" : @"Software\Microsoft\Windows\DWM", group == "gamebar-background" ? "ShowStartupPanel" : "EnableAeroPeek").Exists);
    }

    [Fact]
    public async Task Visual_effect_partial_write_keeps_a_recoverable_backup()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.FailEffectAction = 0x1006;
        var service = fixture.Service();
        Assert.False((await service.ReduceVisualEffectsAsync()).Success);
        Assert.False(fixture.Platform.Effects[0x1002]);
        Assert.True(fixture.Platform.Effects[0x1006]);
        fixture.Platform.FailEffectAction = null;
        Assert.True((await service.UndoAsync(Assert.Single((await service.InspectAsync()).Changes).Id)).Success);
        Assert.All(fixture.Platform.Effects.Values, value => Assert.True(value));
    }
}
