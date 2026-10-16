using Microsoft.Win32;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class OptimizerGuidanceTests
{
    private const string ChromePolicy = @"Software\Policies\Google\Chrome";
    private const string ChromeRegistration = @"Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe";

    [Fact]
    public async Task Startup_is_an_individual_opt_in_and_security_entries_are_not_offered()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Discord", RegistryValueState.String(@"C:\Apps\Discord.exe --startup"));
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Vanguard", RegistryValueState.String(@"C:\Riot Vanguard\vgtray.exe"));
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Windows Security", RegistryValueState.String(@"C:\Windows\System32\SecurityHealthSystray.exe"));
        var preview = await new PcOptimizationService(fixture.Service(), () => false).PreviewAsync();
        var option = Assert.Single(preview.Options, item => item.Id.StartsWith("startup:"));
        Assert.Contains("Discord", option.Title); Assert.False(option.SelectedByDefault);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Only_selected_startup_is_disabled_and_undo_restores_exact_expandable_command()
    {
        using var fixture = new OptimizationFixture();
        var original = RegistryValueState.String(@"%LOCALAPPDATA%\Discord\Update.exe --processStart Discord.exe", RegistryValueKind.ExpandString);
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Discord", original);
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Steam", RegistryValueState.String(@"C:\Steam\Steam.exe -silent"));
        var native = fixture.Service(); var pc = new PcOptimizationService(native, () => false);
        var option = (await pc.PreviewAsync()).Options.Single(item => item.Title.Contains("Discord"));
        Assert.Equal(1, (await pc.ApplyAsync([option.Id])).Applied);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.Run, "Discord").Exists);
        Assert.True(fixture.Platform.ReadValue(OptimizationTargets.Run, "Steam").Exists);
        var backup = Assert.Single((await native.InspectAsync()).Changes);
        Assert.True((await native.UndoAsync(backup.Id)).Success);
        Assert.True(original.Same(fixture.Platform.ReadValue(OptimizationTargets.Run, "Discord")));
    }

    [Fact]
    public async Task Startup_command_changed_after_review_is_preserved_without_a_backup_or_write()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Discord", RegistryValueState.String(@"C:\Apps\Discord.exe --startup"));
        var pc = new PcOptimizationService(fixture.Service(), () => false);
        var option = (await pc.PreviewAsync()).Options.Single(item => item.Id.StartsWith("startup:"));
        var external = RegistryValueState.String(@"C:\Apps\Discord-new.exe --startup");
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Discord", external);
        Assert.Equal(1, (await pc.ApplyAsync([option.Id])).Skipped);
        Assert.True(external.Same(fixture.Platform.ReadValue(OptimizationTargets.Run, "Discord")));
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Startup_command_changed_after_first_step_is_not_removed_by_a_stale_review()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Discord", RegistryValueState.String(@"C:\Apps\Discord.exe"));
        var pc = new PcOptimizationService(fixture.Service(), () => false);
        var option = (await pc.PreviewAsync()).Options.Single(item => item.Id.StartsWith("startup:"));
        var external = RegistryValueState.String(@"C:\Apps\Discord-new.exe");
        var result = await pc.ApplyAsync(["game-mode", option.Id], new ImmediateProgress(progress =>
        {
            if (progress.Completed == 1) fixture.Platform.WriteValue(OptimizationTargets.Run, "Discord", external);
        }));
        Assert.Equal(1, result.Applied); Assert.Equal(1, result.Failed);
        Assert.True(external.Same(fixture.Platform.ReadValue(OptimizationTargets.Run, "Discord")));
        Assert.Single((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Checked_startup_cannot_bypass_the_review_with_a_null_command()
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service();
        var original = RegistryValueState.String(@"C:\Apps\Discord.exe");
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Discord", original);
        var id = (await native.InspectAsync()).StartupEntries.Single().Id;
        Assert.False((await native.DisableStartupCheckedAsync(id, null!)).Success);
        Assert.True(original.Same(fixture.Platform.ReadValue(OptimizationTargets.Run, "Discord")));
        Assert.Empty((await native.InspectAsync()).Changes);
    }

    [Fact]
    public async Task Startup_permission_lost_after_review_is_skipped_without_mutation()
    {
        using var fixture = new OptimizationFixture();
        var original = RegistryValueState.String(@"C:\Apps\Discord.exe");
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Discord", original);
        var pc = new PcOptimizationService(fixture.Service());
        var option = (await pc.PreviewAsync()).Options.Single(item => item.Id.StartsWith("startup:"));
        fixture.Platform.DeniedWriteKeys.Add(OptimizationTargets.Run);
        Assert.Equal(1, (await pc.ApplyAsync([option.Id])).Skipped);
        Assert.True(original.Same(fixture.Platform.ReadValue(OptimizationTargets.Run, "Discord")));
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Run_presence_does_not_claim_effective_activity_when_Windows_has_disabled_startup()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Discord", RegistryValueState.String(@"C:\Apps\Discord.exe"));
        fixture.Platform.WriteValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "Discord",
            new(true, RegistryValueKind.Binary, Bytes: [3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
        var option = (await new PcOptimizationService(fixture.Service()).PreviewAsync()).Options.Single(item => item.Id.StartsWith("startup:"));
        Assert.Contains("sin verificar", option.Status);
        Assert.False(option.SelectedByDefault);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Chrome_background_policy_is_optional_idempotent_and_exactly_reversible(int previousKind)
    {
        using var fixture = new OptimizationFixture();
        var chrome = Path.Combine(fixture.Temp, "chrome.exe"); File.WriteAllText(chrome, "fixture; never execute");
        fixture.Platform.WriteValue(ChromeRegistration, "", RegistryValueState.String(chrome));
        var original = previousKind switch { 0 => RegistryValueState.Absent, 1 => RegistryValueState.Dword(1), _ => RegistryValueState.String("anterior", RegistryValueKind.ExpandString) };
        fixture.Platform.WriteValue(ChromePolicy, "BackgroundModeEnabled", original);
        var native = fixture.Service(); var pc = new PcOptimizationService(native, () => false);
        var option = (await pc.PreviewAsync()).Options.Single(item => item.Id == "chrome-background");
        Assert.True(option.Available); Assert.False(option.SelectedByDefault);
        Assert.Equal(1, (await pc.ApplyAsync([option.Id])).Applied);
        Assert.True(RegistryValueState.Dword(0).Same(fixture.Platform.ReadValue(ChromePolicy, "BackgroundModeEnabled")));
        Assert.Equal(1, (await pc.ApplyAsync([option.Id])).Unchanged);
        Assert.True((await native.UndoAsync(Assert.Single((await native.InspectAsync()).Changes).Id)).Success);
        Assert.True(original.Same(fixture.Platform.ReadValue(ChromePolicy, "BackgroundModeEnabled")));
    }

    [Fact]
    public async Task Chrome_absent_or_unwritable_is_not_applied()
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service();
        var option = (await new PcOptimizationService(native).PreviewAsync()).Options.Single(item => item.Id == "chrome-background");
        Assert.False(option.Available);
        Assert.False((await native.SetToggleAsync("chrome-background", true)).Success);
        var chrome = Path.Combine(fixture.Temp, "chrome.exe"); File.WriteAllText(chrome, "fixture");
        fixture.Platform.WriteValue(ChromeRegistration, "", RegistryValueState.String(chrome));
        fixture.Platform.DeniedWriteKeys.Add(ChromePolicy);
        Assert.False((await native.SetToggleAsync("chrome-background", true)).Success);
        Assert.Empty((await native.InspectAsync()).Changes);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task Chrome_machine_policy_is_respected_without_creating_a_user_override(int machineValue, bool alreadyApplied)
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service();
        var chrome = Path.Combine(fixture.Temp, "chrome.exe"); File.WriteAllText(chrome, "fixture");
        fixture.Platform.WriteValue(ChromeRegistration, "", RegistryValueState.String(chrome));
        fixture.Platform.WriteValue(@"HKLM\SOFTWARE\Policies\Google\Chrome", "BackgroundModeEnabled", RegistryValueState.Dword(machineValue));
        var option = (await new PcOptimizationService(native).PreviewAsync()).Options.Single(item => item.Id == "chrome-background");
        Assert.Equal(alreadyApplied, option.AlreadyApplied);
        if (!alreadyApplied) Assert.False(option.Available);
        Assert.False((await native.SetToggleAsync("chrome-background", true)).Success);
        Assert.False(fixture.Platform.ReadValue(ChromePolicy, "BackgroundModeEnabled").Exists);
        Assert.Empty((await native.InspectAsync()).Changes);
    }

    [Fact]
    public async Task Already_high_performance_gpu_is_detected_and_does_not_rewrite_other_fields()
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service();
        var executable = Path.Combine(fixture.Temp, "game.exe"); File.WriteAllText(executable, "fixture");
        var original = RegistryValueState.String("GpuPreference=2;SwapEffectUpgradeEnable=1;", RegistryValueKind.ExpandString);
        fixture.Platform.WriteValue(OptimizationTargets.GpuPreferences, executable, original);
        var pc = new PcOptimizationService(native); pc.SetApplicationTarget(executable);
        Assert.True((await pc.PreviewAsync()).Options.Single(item => item.Id == "application-gpu").AlreadyApplied);
        Assert.Equal(1, (await pc.ApplyAsync(["application-gpu"])).Unchanged);
        Assert.True(original.Same(fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, executable)));
        Assert.Empty((await native.InspectAsync()).Changes);
    }

    private sealed class ImmediateProgress(Action<PcOptimizationProgress> report) : IProgress<PcOptimizationProgress>
    {
        public void Report(PcOptimizationProgress progress) => report(progress);
    }
}
