using Microsoft.Win32;
using Silen.Core;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class WindowsUpdateHealthTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public void Effective_pause_reported_by_Windows_is_not_hidden_by_missing_UX_dates()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.UpdateService = new(3, true);
        fixture.Platform.WriteValue(@"HKLM\SOFTWARE\Microsoft\WindowsUpdate\UpdatePolicy\Settings", "PausedQualityStatus", RegistryValueState.Dword(1));
        var state = WindowsUpdateDiagnostics.Read(fixture.Platform, Now);
        Assert.True(state.Paused); Assert.False(state.CanSearch);
    }
    [Fact]
    public void Future_pause_is_reported_with_end_date_without_writes()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.UpdateService = new(3, true);
        fixture.Platform.WriteValue(WindowsUpdateDiagnostics.UxKey, "PauseUpdatesExpiryTime", RegistryValueState.String("2026-10-20T12:00:00Z"));
        var state = WindowsUpdateDiagnostics.Read(fixture.Platform, Now);
        Assert.True(state.Paused); Assert.Equal(Now.AddDays(5), state.PausedUntil);
        Assert.False(state.CanSearch); Assert.True(state.NeedsRecovery);
        Assert.True(fixture.Platform.ReadValue(WindowsUpdateDiagnostics.UxKey, "PauseUpdatesExpiryTime").Exists);
    }
    [Fact]
    public void Expired_pause_does_not_block_a_running_manual_service()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.UpdateService = new(3, true);
        fixture.Platform.WriteValue(WindowsUpdateDiagnostics.UxKey, "PauseUpdatesExpiryTime", RegistryValueState.String("2026-10-10T12:00:00Z"));
        Assert.True(WindowsUpdateDiagnostics.Read(fixture.Platform, Now).CanSearch);
    }
    [Theory]
    [InlineData(4, false)]
    [InlineData(3, false)]
    public void Disabled_or_stopped_service_is_not_mistaken_for_no_updates(int start, bool running)
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.UpdateService = new(start, running);
        var state = WindowsUpdateDiagnostics.Read(fixture.Platform, Now);
        Assert.False(state.CanSearch); Assert.True(state.NeedsRecovery); Assert.Equal(running, state.ServiceRunning);
        Assert.Equal(start != 4, state.ServiceEnabled);
    }
    [Fact]
    public void Missing_service_or_unreadable_pause_is_unknown_not_healthy()
    {
        using var fixture = new OptimizationFixture();
        Assert.False(WindowsUpdateDiagnostics.Read(fixture.Platform, Now).Known);
        fixture.Platform.UpdateService = new(3, true);
        fixture.Platform.WriteValue(WindowsUpdateDiagnostics.UxKey, "PauseUpdatesExpiryTime", RegistryValueState.String("not-a-date"));
        Assert.False(WindowsUpdateDiagnostics.Read(fixture.Platform, Now).Known);
    }
    [Fact]
    public void Automatic_update_policy_is_reported_without_falsely_blocking_manual_search()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.UpdateService = new(3, true);
        fixture.Platform.WriteValue(WindowsUpdateDiagnostics.PolicyKey + @"\AU", "NoAutoUpdate", RegistryValueState.Dword(1));
        var state = WindowsUpdateDiagnostics.Read(fixture.Platform, Now);
        Assert.True(state.Managed); Assert.True(state.CanSearch); Assert.False(state.NeedsRecovery);
    }
}
