using Xunit;

namespace Silen.Windows.Tests;

public sealed class WindowsUpdateRecoveryTests
{
    [Fact]
    public async Task Clearing_UX_dates_does_not_claim_resume_if_Windows_still_reports_a_pause()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.UpdateService = new(3, true);
        fixture.Platform.WriteValue(WindowsUpdateDiagnostics.UxKey, "PauseUpdatesExpiryTime", RegistryValueState.String(DateTimeOffset.UtcNow.AddDays(2).ToString("O")));
        fixture.Platform.WriteValue(@"HKLM\SOFTWARE\Microsoft\WindowsUpdate\UpdatePolicy\Settings", "PausedQualityStatus", RegistryValueState.Dword(1));
        var result = await fixture.Service().ResumeWindowsUpdateAsync();
        Assert.False(result.Success); Assert.True(result.Changed);
        Assert.Single((await fixture.Service().InspectAsync()).Changes);
        Assert.True((await fixture.Service().InspectWindowsUpdateAsync()).Paused);
    }
    [Fact]
    public async Task Resume_backs_up_local_pause_and_disabled_service_and_undo_restores_both()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.UpdateService = new(4, false);
        var original = RegistryValueState.String(DateTimeOffset.UtcNow.AddDays(5).ToString("O"));
        fixture.Platform.WriteValue(WindowsUpdateDiagnostics.UxKey, "PauseUpdatesExpiryTime", original);
        var service = fixture.Service();
        var result = await service.ResumeWindowsUpdateAsync();
        Assert.True(result.Success, result.Message); Assert.True(result.Changed);
        Assert.True((await service.InspectWindowsUpdateAsync()).CanSearch);
        var entry = Assert.Single((await service.InspectAsync()).Changes);
        Assert.True((await service.UndoAsync(entry.Id)).Success);
        Assert.Equal(new ServiceSnapshot(4, false), fixture.Platform.UpdateService);
        Assert.Equal(original, fixture.Platform.ReadValue(WindowsUpdateDiagnostics.UxKey, "PauseUpdatesExpiryTime"));
    }
    [Fact]
    public async Task Denied_service_permission_keeps_pause_and_does_not_create_partial_changes()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.UpdateService = new(4, false); fixture.Platform.ServiceAccess = false;
        var original = RegistryValueState.String(DateTimeOffset.UtcNow.AddDays(5).ToString("O"));
        fixture.Platform.WriteValue(WindowsUpdateDiagnostics.UxKey, "PauseUpdatesExpiryTime", original);
        var result = await fixture.Service().ResumeWindowsUpdateAsync();
        Assert.False(result.Success); Assert.False(result.Changed);
        Assert.Equal(original, fixture.Platform.ReadValue(WindowsUpdateDiagnostics.UxKey, "PauseUpdatesExpiryTime"));
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }
    [Fact]
    public async Task Managed_pause_is_not_removed_even_with_service_access()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.UpdateService = new(3, true);
        fixture.Platform.WriteValue(WindowsUpdateDiagnostics.PolicyKey, "PauseQualityUpdates", RegistryValueState.Dword(1));
        Assert.False((await fixture.Service().ResumeWindowsUpdateAsync()).Success);
        Assert.Equal(1, fixture.Platform.ReadValue(WindowsUpdateDiagnostics.PolicyKey, "PauseQualityUpdates").Number);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }
    [Fact]
    public async Task Recovery_undo_preserves_external_service_start_configuration()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.UpdateService = new(4, false);
        var service = fixture.Service();
        await service.ResumeWindowsUpdateAsync();
        var entry = Assert.Single((await service.InspectAsync()).Changes);
        fixture.Platform.UpdateService = new(2, true);
        Assert.False((await service.UndoAsync(entry.Id)).Success);
        Assert.Equal(new ServiceSnapshot(2, true), fixture.Platform.UpdateService);
    }
}
