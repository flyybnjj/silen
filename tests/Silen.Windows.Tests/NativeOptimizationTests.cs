using Microsoft.Win32;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class NativeOptimizationTests
{
    [Fact]
    public async Task Missing_game_mode_preference_is_unknown_and_can_be_applied_and_removed_by_undo()
    {
        using var fixture = new OptimizationFixture();
        var service = fixture.Service();
        Assert.Null((await service.InspectAsync()).Toggles.Single(x => x.Id == "game-mode").Enabled);
        var result = await service.SetToggleAsync("game-mode", true);
        Assert.True(result.Success);
        Assert.True(result.Changed);
        Assert.Equal(1L, fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Number);
        var change = Assert.Single((await service.InspectAsync()).Changes);
        Assert.True((await service.UndoAsync(change.Id)).Success);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Exists);
    }

    [Fact]
    public async Task Game_mode_sets_both_preferences_and_restores_their_distinct_prior_states()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.GameBar, "AllowAutoGameMode", RegistryValueState.Dword(0));
        fixture.Platform.WriteValue(OptimizationTargets.GameBar, "AutoGameModeEnabled", RegistryValueState.Dword(1));
        var service = fixture.Service();
        Assert.Null((await service.InspectAsync()).Toggles.Single(x => x.Id == "game-mode").Enabled);
        Assert.True((await service.SetToggleAsync("game-mode", true)).Success);
        Assert.Equal(1L, fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AllowAutoGameMode").Number);
        Assert.Equal(1L, fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Number);
        Assert.True((await service.UndoAsync(Assert.Single((await service.InspectAsync()).Changes).Id)).Success);
        Assert.Equal(0L, fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AllowAutoGameMode").Number);
        Assert.Equal(1L, fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Number);
    }

    [Fact]
    public async Task Duplicate_targets_in_a_corrupt_journal_are_rejected_before_native_mutation()
    {
        using var fixture = new OptimizationFixture();
        var service = fixture.Service();
        await service.SetToggleAsync("animations", false);
        var path = Path.Combine(fixture.Journal, "optimizations.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        var values = document[0]!["Values"]!.AsArray();
        values.Add(values[0]!.DeepClone());
        await File.WriteAllTextAsync(path, document.ToJsonString());
        var preserved = await File.ReadAllTextAsync(path);
        Assert.False((await service.SetToggleAsync("game-mode", true)).Success);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Exists);
        Assert.Equal(preserved, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Xbox_capture_toggle_restores_mixed_original_types_and_does_not_touch_game_mode()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.GameDvr, "AppCaptureEnabled", RegistryValueState.String("original", RegistryValueKind.ExpandString));
        fixture.Platform.WriteValue(OptimizationTargets.GameBar, "AutoGameModeEnabled", RegistryValueState.Dword(1));
        fixture.Platform.WriteValue(OptimizationTargets.GameBar, "AllowAutoGameMode", RegistryValueState.Dword(1));
        var service = fixture.Service();
        Assert.True((await service.SetToggleAsync("xbox-captures", false)).Success);
        Assert.Equal(0L, fixture.Platform.ReadValue(OptimizationTargets.GameDvr, "AppCaptureEnabled").Number);
        Assert.Equal(0L, fixture.Platform.ReadValue(OptimizationTargets.GameConfig, "GameDVR_Enabled").Number);
        var change = Assert.Single((await service.InspectAsync()).Changes);
        Assert.True((await service.UndoAsync(change.Id)).Success);
        var restored = fixture.Platform.ReadValue(OptimizationTargets.GameDvr, "AppCaptureEnabled");
        Assert.Equal(RegistryValueKind.ExpandString, restored.Kind);
        Assert.Equal("original", restored.Text);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameConfig, "GameDVR_Enabled").Exists);
        Assert.Equal(1L, fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Number);
    }

    [Fact]
    public async Task Animation_toggle_uses_actual_platform_value_and_restores_it()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Animations = true;
        var service = fixture.Service();
        Assert.True((await service.SetToggleAsync("animations", false)).Success);
        Assert.False(fixture.Platform.Animations);
        var change = Assert.Single((await service.InspectAsync()).Changes);
        Assert.True((await service.UndoAsync(change.Id)).Success);
        Assert.True(fixture.Platform.Animations);
    }

    [Fact]
    public async Task Unsupported_animation_read_refuses_mutation_without_a_backup()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Animations = null;
        Assert.False((await fixture.Service().SetToggleAsync("animations", false)).Success);
        Assert.Null(fixture.Platform.Animations);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task External_change_blocks_undo_before_any_of_the_original_values_are_restored()
    {
        using var fixture = new OptimizationFixture();
        var service = fixture.Service();
        await service.SetToggleAsync("xbox-captures", false);
        var change = Assert.Single((await service.InspectAsync()).Changes);
        fixture.Platform.WriteValue(OptimizationTargets.GameConfig, "GameDVR_Enabled", RegistryValueState.Dword(7));
        var result = await service.UndoAsync(change.Id);
        Assert.False(result.Success);
        Assert.Equal(0L, fixture.Platform.ReadValue(OptimizationTargets.GameDvr, "AppCaptureEnabled").Number);
        Assert.Equal(7L, fixture.Platform.ReadValue(OptimizationTargets.GameConfig, "GameDVR_Enabled").Number);
    }

    [Fact]
    public async Task Failure_after_first_write_remains_recoverable_in_another_service_instance()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.FailWriteNumber = 2;
        var result = await fixture.Service().SetToggleAsync("xbox-captures", false);
        Assert.False(result.Success);
        Assert.True(result.Changed);
        fixture.Platform.FailWriteNumber = null;
        var other = fixture.Service();
        var change = Assert.Single((await other.InspectAsync()).Changes);
        Assert.True(change.CanUndo);
        Assert.True((await other.UndoAsync(change.Id)).Success);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameDvr, "AppCaptureEnabled").Exists);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameConfig, "GameDVR_Enabled").Exists);
    }

    [Fact]
    public async Task Journal_is_durable_before_the_first_native_write()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.BeforeWrite = () =>
        {
            var text = File.ReadAllText(Path.Combine(fixture.Journal, "optimizations.json"));
            Assert.Contains("AutoGameModeEnabled", text);
            Assert.Contains("Pending", text);
        };
        Assert.True((await fixture.Service().SetToggleAsync("game-mode", true)).Success);
    }

    [Fact]
    public async Task Corrupt_journal_is_preserved_and_blocks_native_mutation()
    {
        using var fixture = new OptimizationFixture();
        Directory.CreateDirectory(fixture.Journal);
        var path = Path.Combine(fixture.Journal, "optimizations.json");
        await File.WriteAllTextAsync(path, "broken original");
        Assert.False((await fixture.Service().SetToggleAsync("game-mode", true)).Success);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Exists);
        Assert.Equal("broken original", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Two_service_instances_serialize_read_modify_write_and_keep_both_changes()
    {
        using var fixture = new OptimizationFixture();
        var results = await Task.WhenAll(fixture.Service().SetToggleAsync("game-mode", true), fixture.Service().SetToggleAsync("animations", false));
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(2, (await fixture.Service().InspectAsync()).Changes.Count);
    }

    [Fact]
    public async Task Setting_already_selected_state_does_not_create_undo_record()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.GameBar, "AutoGameModeEnabled", RegistryValueState.Dword(1));
        fixture.Platform.WriteValue(OptimizationTargets.GameBar, "AllowAutoGameMode", RegistryValueState.Dword(1));
        var result = await fixture.Service().SetToggleAsync("game-mode", true);
        Assert.True(result.Success);
        Assert.False(result.Changed);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("../../registry")]
    public async Task Unknown_toggle_cannot_become_a_registry_target(string id)
    {
        using var fixture = new OptimizationFixture();
        Assert.False((await fixture.Service().SetToggleAsync(id, true)).Success);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Power_scheme_selection_is_limited_to_installed_schemes_and_undo_restores_previous()
    {
        using var fixture = new OptimizationFixture();
        var first = fixture.Platform.ActivePower;
        var second = Guid.Parse("33333333-3333-3333-3333-333333333333");
        fixture.Platform.Schemes.Add(new(second.ToString(), "OEM rendimiento", false));
        var service = fixture.Service();
        Assert.False((await service.SetPowerSchemeAsync("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")).Success);
        Assert.Equal(first, fixture.Platform.ActivePower);
        Assert.True((await service.SetPowerSchemeAsync(second.ToString())).Success);
        Assert.Equal(second, fixture.Platform.ActivePower);
        Assert.True((await service.UndoAsync(Assert.Single((await service.InspectAsync()).Changes).Id)).Success);
        Assert.Equal(first, fixture.Platform.ActivePower);
    }

    [Fact]
    public async Task Startup_disable_survives_restart_and_reenable_preserves_original_expand_string()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Vendor Launcher", RegistryValueState.String("\"%LOCALAPPDATA%\\app.exe\" --start", RegistryValueKind.ExpandString));
        var service = fixture.Service();
        var entry = Assert.Single((await service.InspectAsync()).StartupEntries);
        Assert.True((await service.SetStartupEnabledAsync(entry.Id, false)).Success);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.Run, "Vendor Launcher").Exists);
        var other = fixture.Service();
        var disabled = Assert.Single((await other.InspectAsync()).StartupEntries);
        Assert.False(disabled.Enabled);
        Assert.True((await other.SetStartupEnabledAsync(disabled.Id, true)).Success);
        var restored = fixture.Platform.ReadValue(OptimizationTargets.Run, "Vendor Launcher");
        Assert.Equal(RegistryValueKind.ExpandString, restored.Kind);
        Assert.Equal("\"%LOCALAPPDATA%\\app.exe\" --start", restored.Text);
    }

    [Fact]
    public async Task Startup_undo_preserves_unrelated_run_entry_and_refuses_replaced_command()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Launcher", RegistryValueState.String("original.exe"));
        var service = fixture.Service();
        await service.SetStartupEnabledAsync(Assert.Single((await service.InspectAsync()).StartupEntries).Id, false);
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Other", RegistryValueState.String("other.exe"));
        fixture.Platform.WriteValue(OptimizationTargets.Run, "Launcher", RegistryValueState.String("replacement.exe"));
        var change = Assert.Single((await service.InspectAsync()).Changes);
        Assert.False((await service.UndoAsync(change.Id)).Success);
        Assert.Equal("replacement.exe", fixture.Platform.ReadValue(OptimizationTargets.Run, "Launcher").Text);
        Assert.Equal("other.exe", fixture.Platform.ReadValue(OptimizationTargets.Run, "Other").Text);
    }

    [Fact]
    public async Task Cancellation_before_mutation_does_not_change_settings()
    {
        using var fixture = new OptimizationFixture();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service().SetToggleAsync("game-mode", true, source.Token));
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Exists);
    }
}

internal sealed class OptimizationFixture : IDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "SilenTests", Guid.NewGuid().ToString("N"));
    internal string Journal => Path.Combine(Root, "journal");
    internal string Temp => Path.Combine(Root, "Temp");
    internal FakeOptimizationPlatform Platform { get; } = new();
    internal NativeOptimizationService Service(int maximumFiles = 5000) => new(Platform, Journal, Temp, () => DateTimeOffset.UtcNow, maximumFiles);
    internal OptimizationFixture() { Directory.CreateDirectory(Temp); }
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}

internal sealed class FakeOptimizationPlatform : IOptimizationPlatform
{
    internal FilterKeysPreference FilterKeys = new(127, 1000, 800, 500, 0);
    internal MouseMotionPreference MouseMotion = new(6, 10, 2);
    internal Action? BeforeInputWrite;
    public FilterKeysPreference GetFilterKeys() => FilterKeys;
    public void SetFilterKeys(FilterKeysPreference value) { BeforeInputWrite?.Invoke(); FilterKeys = value; }
    public MouseMotionPreference GetMouseMotion() => MouseMotion;
    public void SetMouseMotion(MouseMotionPreference value) { BeforeInputWrite?.Invoke(); MouseMotion = value; }
    internal ServiceSnapshot? UpdateService;
    public ServiceSnapshot? ReadUpdateService() => UpdateService;
    public bool CanManageUpdateService() => ServiceAccess && UpdateService is not null;
    public void SetUpdateServiceStartType(int startType)
    {
        if (!CanManageUpdateService()) throw new UnauthorizedAccessException("fixture update service denied");
        UpdateService = UpdateService! with { StartType = startType };
    }
    public void SetUpdateServiceRunning(bool running)
    {
        if (!CanManageUpdateService() || !running && FailServiceStop) throw new UnauthorizedAccessException("fixture update service denied");
        UpdateService = UpdateService! with { Running = running };
    }
    internal Dictionary<string, ServiceSnapshot> Services = new(StringComparer.Ordinal);
    internal bool ServiceAccess = true;
    internal bool FailServiceStop;
    public ServiceSnapshot? ReadService(string name) => Services.GetValueOrDefault(name);
    public bool CanManageService(string name) => ServiceAccess && Services.ContainsKey(name);
    public void SetServiceStartType(string name, int startType)
    {
        if (!CanManageService(name)) throw new UnauthorizedAccessException("fixture service denied");
        Services[name] = Services[name] with { StartType = startType };
    }
    public void SetServiceRunning(string name, bool running)
    {
        if (!CanManageService(name) || !running && FailServiceStop) throw new UnauthorizedAccessException("fixture service denied");
        if (running && Services[name].StartType == 4) throw new InvalidOperationException("disabled service cannot start");
        Services[name] = Services[name] with { Running = running };
    }
    internal Guid? Overlay;
    internal uint? FailEffectAction;
    internal Dictionary<uint, bool> Effects = new() { [0x1002] = true, [0x1004] = true, [0x1006] = true, [0x1012] = true, [0x1016] = true };
    public Guid GetPowerMode() => Overlay ?? throw new InvalidOperationException("unsupported overlay");
    public void SetPowerMode(Guid id) => Overlay = id;
    public bool GetVisualEffect(uint action) => Effects[action];
    public void SetVisualEffect(uint action, bool enabled)
    {
        if (action == FailEffectAction) throw new UnauthorizedAccessException("effect fixture denied");
        Effects[action] = enabled;
    }
    private readonly Dictionary<string, RegistryValueState> values = new(StringComparer.OrdinalIgnoreCase);
    internal bool? Animations = true;
    internal Guid ActivePower = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal List<PowerScheme> Schemes = [new("11111111-1111-1111-1111-111111111111", "Equilibrado", true)];
    internal int? FailWriteNumber;
    internal Action? BeforeWrite;
    private int writes;
    internal HashSet<string> DeniedWriteKeys = new(StringComparer.OrdinalIgnoreCase);
    public bool CanWriteKey(string path) => !DeniedWriteKeys.Any(key => path.Equals(key, StringComparison.OrdinalIgnoreCase) || path.StartsWith(key + "\\", StringComparison.OrdinalIgnoreCase));
    public RegistryValueState ReadValue(string path, string name) { lock (values) return values.GetValueOrDefault(path + "|" + name) ?? RegistryValueState.Absent; }
    public void WriteValue(string path, string name, RegistryValueState value)
    {
        BeforeWrite?.Invoke();
        if (!CanWriteKey(path)) throw new UnauthorizedAccessException("fixture registry policy denied");
        if (++writes == FailWriteNumber) throw new UnauthorizedAccessException("fixture denied write");
        lock (values) { if (value.Exists) values[path + "|" + name] = value; else values.Remove(path + "|" + name); }
    }
    public IReadOnlyDictionary<string, RegistryValueState> ReadRunEntries()
    {
        lock (values) return values.Where(x => x.Key.StartsWith(OptimizationTargets.Run + "|", StringComparison.OrdinalIgnoreCase)).ToDictionary(x => x.Key[(OptimizationTargets.Run.Length + 1)..], x => x.Value);
    }
    public bool? GetAnimations() => Animations;
    public void SetAnimations(bool enabled) => Animations = enabled;
    public List<PowerScheme> ReadPowerSchemes() => Schemes.Select(x => x with { Active = Guid.Parse(x.Id) == ActivePower }).ToList();
    public Guid GetActivePowerScheme() => ActivePower;
    public void SetActivePowerScheme(Guid id) { if (!Schemes.Any(x => Guid.Parse(x.Id) == id)) throw new InvalidOperationException("not installed"); ActivePower = id; }
}
