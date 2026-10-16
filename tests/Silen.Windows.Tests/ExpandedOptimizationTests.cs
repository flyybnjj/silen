using Microsoft.Win32;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class ExpandedOptimizationTests
{
    [Fact]
    public async Task Restricted_basic_preference_is_unavailable_before_creating_a_backup()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.DeniedWriteKeys.Add(OptimizationTargets.GameBar);
        var service = fixture.Service();
        Assert.False((await service.InspectAsync()).Toggles.Single(toggle => toggle.Id == "game-mode").CanEnable);
        Assert.False((await service.SetToggleAsync("game-mode", true)).Success);
        Assert.Empty((await service.InspectAsync()).Changes);
    }
    [Theory]
    [InlineData("service-diagtrack", "DiagTrack", 2, true)]
    [InlineData("service-maps", "MapsBroker", 2, false)]
    [InlineData("service-fax", "Fax", 3, false)]
    public async Task Optional_service_restores_start_type_and_original_running_state(string id, string name, int start, bool running)
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Services[name] = new(start, running);
        var service = fixture.Service();
        Assert.True((await service.SetToggleAsync(id, true)).Success);
        Assert.Equal(new ServiceSnapshot(4, false), fixture.Platform.Services[name]);
        Assert.True((await service.SetToggleAsync(id, false)).Success);
        Assert.Equal(new ServiceSnapshot(start, running), fixture.Platform.Services[name]);
    }

    [Fact]
    public async Task Service_stop_failure_preserves_a_recoverable_start_type_backup()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Services["DiagTrack"] = new(2, true);
        fixture.Platform.FailServiceStop = true;
        var service = fixture.Service();
        Assert.False((await service.SetToggleAsync("service-diagtrack", true)).Success);
        Assert.Equal(new ServiceSnapshot(4, true), fixture.Platform.Services["DiagTrack"]);
        fixture.Platform.FailServiceStop = false;
        Assert.True((await service.UndoAsync(Assert.Single((await service.InspectAsync()).Changes).Id)).Success);
        Assert.Equal(new ServiceSnapshot(2, true), fixture.Platform.Services["DiagTrack"]);
    }

    [Fact]
    public async Task Services_absent_or_without_permission_are_unavailable_without_changes()
    {
        using var fixture = new OptimizationFixture();
        var service = fixture.Service();
        Assert.False((await service.SetToggleAsync("service-fax", true)).Success);
        Assert.False((await service.InspectAsync()).Toggles.Single(toggle => toggle.Id == "service-fax").CanEnable);
        fixture.Platform.Services["MapsBroker"] = new(3, true);
        fixture.Platform.ServiceAccess = false;
        Assert.False((await service.SetToggleAsync("service-maps", true)).Success);
        Assert.Equal(new ServiceSnapshot(3, true), fixture.Platform.Services["MapsBroker"]);
        Assert.Empty((await service.InspectAsync()).Changes);
    }

    [Fact]
    public async Task Service_undo_does_not_overwrite_an_external_configuration()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Services["DiagTrack"] = new(2, true);
        var service = fixture.Service();
        await service.SetToggleAsync("service-diagtrack", true);
        fixture.Platform.Services["DiagTrack"] = new(3, false);
        Assert.False((await service.UndoAsync(Assert.Single((await service.InspectAsync()).Changes).Id)).Success);
        Assert.Equal(new ServiceSnapshot(3, false), fixture.Platform.Services["DiagTrack"]);
    }

    [Fact]
    public async Task Disabled_but_still_running_service_is_not_stopped_without_a_restorable_start()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Services["DiagTrack"] = new(4, true);
        Assert.False((await fixture.Service().SetToggleAsync("service-diagtrack", true)).Success);
        Assert.Equal(new ServiceSnapshot(4, true), fixture.Platform.Services["DiagTrack"]);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Critical_service_names_are_not_mutation_targets()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Services["WinDefend"] = new(2, true);
        Assert.False((await fixture.Service().SetToggleAsync("service-WinDefend", true)).Success);
        Assert.Equal(new ServiceSnapshot(2, true), fixture.Platform.Services["WinDefend"]);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Power_throttling_policy_rechecks_ac_before_writes()
    {
        using var fixture = new OptimizationFixture();
        var calls = 0;
        var service = new NativeOptimizationService(fixture.Platform, fixture.Journal, fixture.Temp, () => DateTimeOffset.UtcNow, onAcPower: () => ++calls < 3);
        Assert.False((await service.SetToggleAsync("power-throttling", true)).Success);
        Assert.False(fixture.Platform.ReadValue(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff").Exists);
    }

    [Fact]
    public async Task Inspection_exposes_implemented_preferences_without_applying_them()
    {
        using var fixture = new OptimizationFixture();
        var overview = await fixture.Service().InspectAsync();
        foreach (var id in new[] { "browser-background", "background-apps", "gamebar-background", "desktop-composition", "content-suggestions", "diagnostic-personalization", "online-search" })
            Assert.Contains(overview.Toggles, toggle => toggle.Id == id && !string.IsNullOrWhiteSpace(toggle.Description));
        Assert.Empty(overview.Changes);
        Assert.False(fixture.Platform.ReadValue(@"Software\Policies\Microsoft\Edge", "StartupBoostEnabled").Exists);
    }

    [Fact]
    public async Task Applying_and_restoring_an_exposed_preference_preserves_exact_original_type()
    {
        using var fixture = new OptimizationFixture();
        const string key = @"Software\Policies\Microsoft\Edge";
        fixture.Platform.WriteValue(key, "StartupBoostEnabled", RegistryValueState.String("original", RegistryValueKind.ExpandString));
        var service = fixture.Service();
        Assert.True((await service.SetToggleAsync("browser-background", true)).Success);
        Assert.Equal(0, fixture.Platform.ReadValue(key, "StartupBoostEnabled").Number);
        Assert.True((await service.SetToggleAsync("browser-background", false)).Success);
        Assert.Equal(RegistryValueKind.ExpandString, fixture.Platform.ReadValue(key, "StartupBoostEnabled").Kind);
        Assert.Equal("original", fixture.Platform.ReadValue(key, "StartupBoostEnabled").Text);
        Assert.False(fixture.Platform.ReadValue(key, "BackgroundModeEnabled").Exists);
    }

    [Fact]
    public async Task An_existing_preference_without_a_backup_cannot_invent_a_restore_value()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(@"Software\Policies\Microsoft\Edge", "StartupBoostEnabled", RegistryValueState.Dword(0));
        fixture.Platform.WriteValue(@"Software\Policies\Microsoft\Edge", "BackgroundModeEnabled", RegistryValueState.Dword(0));
        Assert.False((await fixture.Service().SetToggleAsync("browser-background", false)).Success);
        Assert.Equal(0, fixture.Platform.ReadValue(@"Software\Policies\Microsoft\Edge", "StartupBoostEnabled").Number);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Delivery_optimization_uses_machine_scope_and_restores_absent_policy()
    {
        using var fixture = new OptimizationFixture();
        var service = fixture.Service();
        Assert.True((await service.ApplyPerformanceTweakAsync("delivery-optimization")).Success);
        const string key = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
        Assert.True(fixture.Platform.ReadValue(key, "DODownloadMode").Exists);
        Assert.Equal(0, fixture.Platform.ReadValue(key, "DODownloadMode").Number);
        Assert.False(fixture.Platform.ReadValue(key[5..], "DODownloadMode").Exists);
        Assert.True((await service.UndoAsync(Assert.Single((await service.InspectAsync()).Changes).Id)).Success);
        Assert.False(fixture.Platform.ReadValue(key, "DODownloadMode").Exists);
    }

    [Fact]
    public async Task Restricted_machine_policy_is_not_written_or_journaled()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.DeniedWriteKeys.Add(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization");
        var service = fixture.Service();
        Assert.False((await service.ApplyPerformanceTweakAsync("delivery-optimization")).Success);
        Assert.Empty((await service.InspectAsync()).Changes);
    }
}
