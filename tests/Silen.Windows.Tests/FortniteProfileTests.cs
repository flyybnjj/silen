using Microsoft.Win32;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class FortniteProfileTests
{
    [Fact]
    public async Task Locked_Edge_policy_is_unavailable_and_other_default_steps_complete()
    {
        using var fixture = new ProfileFixture();
        fixture.Platform.DeniedWriteKeys.Add(@"Software\Policies\Microsoft");
        var profile = fixture.Profile;
        var preview = await profile.PreviewAsync();
        var browser = preview.Options.Single(option => option.Id == "browser-background");
        Assert.False(browser.Available);
        Assert.False(browser.SelectedByDefault);
        Assert.Contains("permiso", browser.Status, StringComparison.OrdinalIgnoreCase);
        var selected = preview.Options.Where(option => option.Available && option.SelectedByDefault).Select(option => option.Id).ToArray();
        var result = await profile.ApplyAsync(selected);
        Assert.All(result.Steps, step => Assert.True(step.Success, step.Message));
        Assert.DoesNotContain(result.Steps, step => step.Id == "browser-background");
        Assert.False(fixture.Platform.ReadValue(@"Software\Policies\Microsoft\Edge", "StartupBoostEnabled").Exists);
        Assert.Contains((await fixture.Native.InspectAsync()).Changes, change => change.Title.Contains("Modo Juego"));
    }

    [Fact]
    public async Task Edge_permission_loss_after_preview_does_not_create_a_failed_backup()
    {
        using var fixture = new ProfileFixture();
        var profile = fixture.Profile;
        var preview = await profile.PreviewAsync();
        Assert.True(preview.Options.Single(option => option.Id == "browser-background").Available);
        fixture.Platform.DeniedWriteKeys.Add(@"Software\Policies\Microsoft\Edge");
        var result = await profile.ApplyAsync(["browser-background", "winutil-game-mode"]);
        Assert.False(result.Steps.Single(step => step.Id == "browser-background").Success);
        Assert.True(result.Steps.Single(step => step.Id == "winutil-game-mode").Success);
        Assert.DoesNotContain((await fixture.Native.InspectAsync()).Changes, change => change.Title.Contains("Edge"));
    }

    [Fact]
    public async Task Official_WinUtil_selection_sets_both_game_mode_values_and_restores_originals()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.GameBar, "AllowAutoGameMode", RegistryValueState.String("original"));
        var native = fixture.Service();
        Assert.True((await native.ApplyWinUtilGameModeAsync()).Success);
        Assert.Equal(1, fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AllowAutoGameMode").Number);
        Assert.Equal(1, fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Number);
        var change = Assert.Single((await native.InspectAsync()).Changes);
        Assert.Contains("WinUtil", change.Title);
        Assert.True((await native.UndoAsync(change.Id)).Success);
        Assert.Equal("original", fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AllowAutoGameMode").Text);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Exists);
    }

    [Fact]
    public async Task Fortnite_gpu_preference_preserves_other_tokens_and_restores_exact_prior_value()
    {
        using var fixture = new ProfileFixture();
        var original = RegistryValueState.String("CustomFlag=9;GpuPreference=1;Other=keep;", RegistryValueKind.ExpandString);
        fixture.Platform.WriteValue(OptimizationTargets.GpuPreferences, fixture.Executable, original);
        var native = fixture.Native;
        Assert.True((await native.SetGameGpuPreferenceAsync(fixture.Executable)).Success);
        Assert.Equal("CustomFlag=9;GpuPreference=2;Other=keep;", fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, fixture.Executable).Text);
        Assert.True(native.ReadGameGpuPreference(fixture.Executable));
        Assert.True((await native.UndoAsync(Assert.Single((await native.InspectAsync()).Changes).Id)).Success);
        Assert.True(original.Same(fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, fixture.Executable)));
    }

    [Fact]
    public async Task Fortnite_gpu_preference_absence_can_be_restored_from_another_instance()
    {
        using var fixture = new ProfileFixture();
        Assert.True((await fixture.Native.SetGameGpuPreferenceAsync(fixture.Executable)).Success);
        var other = fixture.Native;
        Assert.True((await other.UndoAsync(Assert.Single((await other.InspectAsync()).Changes).Id)).Success);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, fixture.Executable).Exists);
    }

    [Fact]
    public async Task Fortnite_gpu_preference_rejects_foreign_executable_and_duplicate_preference_tokens()
    {
        using var fixture = new ProfileFixture();
        var foreign = Path.Combine(fixture.Root, "arbitrary.exe");
        File.WriteAllText(foreign, "fixture");
        Assert.False((await fixture.Native.SetGameGpuPreferenceAsync(foreign)).Success);
        fixture.Platform.WriteValue(OptimizationTargets.GpuPreferences, fixture.Executable, RegistryValueState.String("GpuPreference=1;GpuPreference=0;"));
        Assert.False((await fixture.Native.SetGameGpuPreferenceAsync(fixture.Executable)).Success);
        Assert.Empty((await fixture.Native.InspectAsync()).Changes);
    }

    [Fact]
    public async Task Fortnite_gpu_undo_preserves_an_external_preference_change()
    {
        using var fixture = new ProfileFixture();
        await fixture.Native.SetGameGpuPreferenceAsync(fixture.Executable);
        fixture.Platform.WriteValue(OptimizationTargets.GpuPreferences, fixture.Executable, RegistryValueState.String("GpuPreference=0;External=1;"));
        Assert.False((await fixture.Native.UndoAsync(Assert.Single((await fixture.Native.InspectAsync()).Changes).Id)).Success);
        Assert.Equal("GpuPreference=0;External=1;", fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, fixture.Executable).Text);
    }

    [Fact]
    public async Task Preview_is_read_only_and_does_not_require_a_scan()
    {
        using var fixture = new ProfileFixture();
        var preview = await fixture.Profile.PreviewAsync();
        Assert.True(preview.Installation.Installed);
        Assert.Equal(12, preview.Options.Count);
        Assert.False(preview.Options.Single(x => x.Id == "animations").SelectedByDefault);
        Assert.Empty((await fixture.Native.InspectAsync()).Changes);
        Assert.Equal(0, fixture.Game.Applied);
    }

    [Fact]
    public async Task Automatic_performance_profile_preserves_privacy_and_search_preferences()
    {
        using var fixture = new ProfileFixture();
        const string advertising = @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo";
        fixture.Platform.WriteValue(advertising, "Enabled", RegistryValueState.Dword(1));
        var profile = fixture.Profile;
        var preview = await profile.PreviewAsync();
        var result = await profile.ApplyAsync(preview.Options.Where(option => option.Available && option.SelectedByDefault).Select(option => option.Id).ToArray());
        Assert.All(result.Steps, step => Assert.True(step.Success, step.Message));
        Assert.Equal(1, fixture.Platform.ReadValue(advertising, "Enabled").Number);
        Assert.False(fixture.Platform.ReadValue(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled").Exists);
        Assert.False(fixture.Platform.Effects[0x1002]);
        Assert.Equal(0, fixture.Platform.ReadValue(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AudioCaptureEnabled").Number);
    }

    [Fact]
    public async Task Readonly_game_settings_are_unavailable_with_the_actual_reason_in_preview()
    {
        using var fixture = new FortniteGameTests.GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        File.SetAttributes(fixture.Settings, FileAttributes.ReadOnly);
        var native = new NativeOptimizationService(new FakeOptimizationPlatform(), Path.Combine(fixture.Root, "native-journal"), Path.Combine(fixture.Root, "temp"), () => DateTimeOffset.UtcNow);
        var profile = new FortniteProfileService(native, fixture.Service(), () => true, native.SetGameGpuPreferenceAsync, native.ReadGameGpuPreference, native.ApplyWinUtilGameModeAsync);
        var option = (await profile.PreviewAsync()).Options.Single(x => x.Id == "fortnite-settings");
        Assert.False(option.Available);
        Assert.False(option.SelectedByDefault);
        Assert.Contains("solo lectura", option.Status);
    }

    [Fact]
    public async Task Applying_only_selected_steps_changes_game_mode_and_captures_but_not_power_or_game()
    {
        using var fixture = new ProfileFixture();
        var profile = fixture.Profile;
        await profile.PreviewAsync();
        var result = await profile.ApplyAsync(["winutil-game-mode", "xbox-captures"]);
        Assert.All(result.Steps, x => Assert.True(x.Success));
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal(1, fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AllowAutoGameMode").Number);
        Assert.Equal(0, fixture.Platform.ReadValue(OptimizationTargets.GameDvr, "AppCaptureEnabled").Number);
        Assert.Equal(fixture.OriginalPower, fixture.Platform.ActivePower);
        Assert.True(fixture.Platform.Animations);
        Assert.Equal(0, fixture.Game.Applied);
    }

    [Fact]
    public async Task Applying_without_preview_or_with_unknown_step_makes_no_changes()
    {
        using var fixture = new ProfileFixture();
        var profile = fixture.Profile;
        Assert.False(Assert.Single((await profile.ApplyAsync(["winutil-game-mode"])).Steps).Success);
        await profile.PreviewAsync();
        Assert.False(Assert.Single((await profile.ApplyAsync(["winutil-game-mode", "remote-script"])).Steps).Success);
        Assert.Empty((await fixture.Native.InspectAsync()).Changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Power_is_unavailable_without_confirmed_AC(bool? ac)
    {
        using var fixture = new ProfileFixture { Ac = ac };
        var profile = fixture.Profile;
        var preview = await profile.PreviewAsync();
        Assert.False(preview.Options.Single(x => x.Id == "power").Available);
        var result = await profile.ApplyAsync(["power"]);
        Assert.False(Assert.Single(result.Steps).Changed);
        Assert.Equal(fixture.OriginalPower, fixture.Platform.ActivePower);
    }

    [Fact]
    public async Task Switching_to_battery_after_preview_does_not_apply_high_performance()
    {
        using var fixture = new ProfileFixture();
        var profile = fixture.Profile;
        Assert.True((await profile.PreviewAsync()).Options.Single(x => x.Id == "power").Available);
        fixture.Ac = false;
        Assert.False(Assert.Single((await profile.ApplyAsync(["power"])).Steps).Changed);
        Assert.Equal(fixture.OriginalPower, fixture.Platform.ActivePower);
    }

    [Theory]
    [InlineData("power")]
    [InlineData("gpu-preference")]
    public async Task Switching_to_battery_while_another_instance_holds_the_journal_blocks_mutation(string step)
    {
        using var fixture = new ProfileFixture();
        var profile = fixture.Profile;
        await profile.PreviewAsync();
        await using var lease = await new OptimizationJournal(Path.Combine(fixture.Root, "journal")).LeaseAsync(CancellationToken.None);
        var applying = profile.ApplyAsync([step]);
        Assert.False(applying.IsCompleted);
        fixture.Ac = false;
        await lease.DisposeAsync();
        var result = await applying;
        Assert.False(Assert.Single(result.Steps).Changed);
        Assert.Equal(fixture.OriginalPower, fixture.Platform.ActivePower);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, fixture.Executable).Exists);
    }

    [Fact]
    public async Task No_high_performance_plan_means_no_new_plan_is_created()
    {
        using var fixture = new ProfileFixture();
        fixture.Platform.Schemes.RemoveAll(x => x.Id == FortniteProfileService.HighPerformanceScheme);
        var preview = await fixture.Profile.PreviewAsync();
        Assert.False(preview.Options.Single(x => x.Id == "power").Available);
        Assert.Single(fixture.Platform.Schemes);
    }

    [Fact]
    public async Task Cancellation_preserves_completed_results_and_durable_windows_changes()
    {
        using var fixture = new ProfileFixture();
        using var cancellation = new CancellationTokenSource();
        var profile = fixture.Profile;
        await profile.PreviewAsync();
        var progress = new InlineProgress<FortniteProfileProgress>(x => { if (x.Completed == 1) cancellation.Cancel(); });
        var result = await profile.ApplyAsync(["winutil-game-mode", "xbox-captures"], progress, cancellation.Token);
        Assert.True(result.Cancelled);
        Assert.True(result.Steps[0].Changed);
        Assert.Single((await fixture.Native.InspectAsync()).Changes);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameDvr, "AppCaptureEnabled").Exists);
    }

    [Fact]
    public async Task Partial_failure_is_reported_and_remaining_independent_steps_finish()
    {
        using var fixture = new ProfileFixture();
        fixture.Game.Outcome = new(false, "El juego está abierto.");
        var profile = fixture.Profile;
        await profile.PreviewAsync();
        var result = await profile.ApplyAsync(["winutil-game-mode", "fortnite-settings"]);
        Assert.True(result.Steps.Single(x => x.Id == "winutil-game-mode").Success);
        Assert.False(result.Steps.Single(x => x.Id == "fortnite-settings").Success);
        Assert.Contains("No se completaron", result.Message);
    }

    [Fact]
    public async Task Profile_undo_routes_game_changes_and_windows_changes_to_their_own_store()
    {
        using var fixture = new ProfileFixture();
        var profile = fixture.Profile;
        await profile.PreviewAsync();
        await profile.ApplyAsync(["winutil-game-mode", "fortnite-settings"]);
        var preview = await profile.PreviewAsync();
        Assert.Equal(2, preview.Changes.Count);
        Assert.True((await profile.UndoAsync("fortnite:fixture-change")).Success);
        Assert.Equal(1, fixture.Game.Undone);
        var windows = preview.Changes.Single(x => !x.Id.StartsWith("fortnite:"));
        Assert.True((await profile.UndoAsync(windows.Id)).Success);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AllowAutoGameMode").Exists);
    }

    private sealed class ProfileFixture : IDisposable
    {
        private readonly OptimizationFixture fixture = new();
        public string Root => fixture.Root;
        public FakeOptimizationPlatform Platform => fixture.Platform;
        public NativeOptimizationService Native => fixture.Service();
        public Guid OriginalPower => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string Executable { get; }
        public bool? Ac = true;
        public FixtureGame Game { get; }
        public FortniteProfileService Profile
        {
            get
            {
                var native = Native;
                return new(native, Game, () => Ac, native.SetGameGpuPreferenceAsync, native.ReadGameGpuPreference, native.ApplyWinUtilGameModeAsync);
            }
        }
        public ProfileFixture()
        {
            Executable = Path.Combine(Root, "Fortnite", "FortniteGame", "Binaries", "Win64", "FortniteClient-Win64-Shipping.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(Executable)!);
            File.WriteAllText(Executable, "fixture");
            Platform.Schemes.Add(new(FortniteProfileService.HighPerformanceScheme, "High performance", false));
            var settings = Path.Combine(Root, "GameUserSettings.ini");
            File.WriteAllText(settings, "[ScalabilityGroups]\nsg.ShadowQuality=3\n");
            Game = new(new(true, Path.Combine(Root, "Fortnite"), Executable, settings, false, "Fixture detectada"));
        }
        public void Dispose() => fixture.Dispose();
    }

    private sealed class FixtureGame(FortniteInstallation installation) : IFortniteGameService
    {
        public int Applied;
        public int Undone;
        public OptimizationOutcome Outcome = new(true, "Calidad baja aplicada con respaldo.", true);
        public Task<FortniteInstallation> DetectAsync(string? installDirectory = null, CancellationToken cancellationToken = default) => Task.FromResult(installation);
        public Task<OptimizationOutcome> ApplyCompetitiveSettingsAsync(CancellationToken cancellationToken = default) { Applied++; return Task.FromResult(Outcome); }
        public Task<List<AppliedOptimization>> ReadChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Applied > Undone && Outcome.Changed ? new List<AppliedOptimization> { new("fortnite:fixture-change", "Fortnite", DateTimeOffset.UtcNow, true, "Fixture") } : []);
        public Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default) { Undone++; return Task.FromResult(new OptimizationOutcome(true, "Restaurado", true)); }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
}
