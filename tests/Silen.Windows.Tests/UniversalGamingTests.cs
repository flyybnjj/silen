using Microsoft.Win32;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class UniversalGamingTests
{
    [Fact]
    public async Task Any_selected_game_gets_a_reversible_GPU_preference_without_replacing_other_tokens()
    {
        using var fixture = new OptimizationFixture();
        var executable = Path.Combine(fixture.Temp, "Other Game.exe");
        await File.WriteAllTextAsync(executable, "fixture; never executed");
        var previous = RegistryValueState.String("SwapEffectUpgradeEnable=1;GpuPreference=1;", RegistryValueKind.ExpandString);
        fixture.Platform.WriteValue(OptimizationTargets.GpuPreferences, executable, previous);
        INativeOptimizationService service = fixture.Service();
        Assert.True((await service.SetApplicationGpuPreferenceAsync(executable)).Success);
        Assert.Equal("SwapEffectUpgradeEnable=1;GpuPreference=2;", fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, executable).Text);
        File.Delete(executable);
        Assert.True((await service.UndoAsync(Assert.Single((await service.InspectAsync()).Changes).Id)).Success);
        Assert.Equal(previous, fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, executable));
    }
    [Fact]
    public async Task GPU_preference_requires_an_existing_executable_and_does_not_invent_missing_files()
    {
        using var fixture = new OptimizationFixture();
        INativeOptimizationService service = fixture.Service();
        Assert.False((await service.SetApplicationGpuPreferenceAsync(Path.Combine(fixture.Temp, "missing.exe"))).Success);
        var text = Path.Combine(fixture.Temp, "settings.ini"); await File.WriteAllTextAsync(text, "fixture");
        Assert.False((await service.SetApplicationGpuPreferenceAsync(text)).Success);
        Assert.Empty((await service.InspectAsync()).Changes);
    }
    [Fact]
    public async Task Background_bandwidth_limit_is_machine_scoped_and_restorable()
    {
        using var fixture = new OptimizationFixture();
        var service = fixture.Service();
        Assert.True((await service.ApplyPerformanceTweakAsync("gaming-network-bandwidth")).Success);
        const string key = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
        Assert.Equal(10, fixture.Platform.ReadValue(key, "DOPercentageMaxBackgroundBandwidth").Number);
        Assert.True((await service.UndoAsync(Assert.Single((await service.InspectAsync()).Changes).Id)).Success);
        Assert.False(fixture.Platform.ReadValue(key, "DOPercentageMaxBackgroundBandwidth").Exists);
    }
}
