using Microsoft.Win32;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class PerformanceTweaksTests
{
    [Fact]
    public async Task Readonly_Edge_values_already_at_target_are_successful_without_writes()
    {
        using var fixture = new OptimizationFixture();
        const string key = @"Software\Policies\Microsoft\Edge";
        fixture.Platform.WriteValue(key, "StartupBoostEnabled", RegistryValueState.Dword(0));
        fixture.Platform.WriteValue(key, "BackgroundModeEnabled", RegistryValueState.Dword(0));
        fixture.Platform.DeniedWriteKeys.Add(key);
        var service = fixture.Service();
        Assert.True(service.CanApplyPerformanceTweak("browser-background"));
        var result = await service.ApplyPerformanceTweakAsync("browser-background");
        Assert.True(result.Success);
        Assert.False(result.Changed);
        Assert.Empty((await service.InspectAsync()).Changes);
    }

    [Fact]
    public async Task Background_and_browser_preferences_have_exact_reversible_backups()
    {
        using var fixture = new OptimizationFixture();
        var service = fixture.Service();
        foreach (var id in new[] { "background-apps", "browser-background", "content-suggestions", "diagnostic-personalization", "online-search" })
        {
            var result = await service.ApplyPerformanceTweakAsync(id);
            Assert.True(result.Success, result.Message);
            Assert.True(result.Changed);
        }
        Assert.Equal(1, fixture.Platform.ReadValue(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled").Number);
        Assert.Equal(0, fixture.Platform.ReadValue(@"Software\Policies\Microsoft\Edge", "StartupBoostEnabled").Number);
        Assert.Equal(0, fixture.Platform.ReadValue(@"Software\Policies\Microsoft\Edge", "BackgroundModeEnabled").Number);
        var changes = (await service.InspectAsync()).Changes;
        Assert.Equal(5, changes.Count);
        foreach (var change in changes) Assert.True((await service.UndoAsync(change.Id)).Success);
        Assert.False(fixture.Platform.ReadValue(@"Software\Policies\Microsoft\Edge", "StartupBoostEnabled").Exists);
        Assert.False(fixture.Platform.ReadValue(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled").Exists);
    }

    [Fact]
    public async Task Multi_value_preference_preserves_types_and_rejects_external_changes_on_undo()
    {
        using var fixture = new OptimizationFixture();
        const string key = @"Software\Policies\Microsoft\Edge";
        fixture.Platform.WriteValue(key, "StartupBoostEnabled", RegistryValueState.String("old", RegistryValueKind.ExpandString));
        var service = fixture.Service();
        await service.ApplyPerformanceTweakAsync("browser-background");
        var id = Assert.Single((await service.InspectAsync()).Changes).Id;
        fixture.Platform.WriteValue(key, "BackgroundModeEnabled", RegistryValueState.Dword(1));
        Assert.False((await service.UndoAsync(id)).Success);
        Assert.Equal(0, fixture.Platform.ReadValue(key, "StartupBoostEnabled").Number);
        fixture.Platform.WriteValue(key, "BackgroundModeEnabled", RegistryValueState.Dword(0));
        Assert.True((await service.UndoAsync(id)).Success);
        Assert.Equal("old", fixture.Platform.ReadValue(key, "StartupBoostEnabled").Text);
        Assert.Equal(RegistryValueKind.ExpandString, fixture.Platform.ReadValue(key, "StartupBoostEnabled").Kind);
    }

    [Fact]
    public async Task Unknown_group_has_no_mutations()
    {
        using var fixture = new OptimizationFixture();
        Assert.False((await fixture.Service().ApplyPerformanceTweakAsync("remote-script")).Success);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }
}
