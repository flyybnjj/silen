using Xunit;

namespace Silen.Windows.Tests;

public sealed class InstalledProductsTests
{
    [Fact]
    public void Epic_manifest_locates_any_game_and_rejects_an_executable_outside_its_installation()
    {
        using var fixture = new OptimizationFixture();
        var product = InstalledProductCatalog.ParseEpic(System.Text.Json.JsonSerializer.Serialize(new { DisplayName = "Otro juego", InstallLocation = fixture.Temp, LaunchExecutable = "bin/game.exe" }));
        Assert.Equal("Otro juego", product!.Name); Assert.EndsWith("game.exe", product.Executable);
        Assert.Null(InstalledProductCatalog.ParseEpic(System.Text.Json.JsonSerializer.Serialize(new { DisplayName = "Inseguro", InstallLocation = fixture.Temp, LaunchExecutable = "../outside.exe" }))?.Executable);
    }
    [Fact]
    public async Task Product_search_is_bounded_to_the_installation_and_does_not_offer_uninstallers()
    {
        using var fixture = new OptimizationFixture();
        Directory.CreateDirectory(Path.Combine(fixture.Temp, "bin"));
        var game = Path.Combine(fixture.Temp, "bin", "game.exe"); File.WriteAllText(game, "fixture; never execute");
        File.WriteAllText(Path.Combine(fixture.Temp, "uninstall.exe"), "fixture");
        var catalog = new InstalledProductCatalog();
        var found = await catalog.LocateExecutablesAsync(new("test", "Juego", "Prueba", fixture.Temp, null, "Prueba"));
        Assert.Single(found); Assert.Equal(game, found[0]);
    }
    [Fact]
    public async Task Main_optimizer_uses_the_chosen_executable_with_backup_and_requires_a_new_preview_after_target_changes()
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service();
        var first = Path.Combine(fixture.Temp, "first.exe"); var second = Path.Combine(fixture.Temp, "second.exe");
        File.WriteAllText(first, "fixture"); File.WriteAllText(second, "fixture");
        IPcOptimizationService optimizer = new PcOptimizationService(native, () => false);
        optimizer.SetApplicationTarget(first);
        Assert.Contains((await optimizer.PreviewAsync()).Options, option => option.Id == "application-gpu");
        optimizer.SetApplicationTarget(second);
        Assert.Equal(1, (await optimizer.ApplyAsync(["application-gpu"])).Failed);
        await optimizer.PreviewAsync(); Assert.Equal(1, (await optimizer.ApplyAsync(["application-gpu"])).Applied);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, first).Exists);
        Assert.Contains("GpuPreference=2", fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, second).Text);
        Assert.True((await native.UndoAsync(Assert.Single((await native.InspectAsync()).Changes).Id)).Success);
    }
    [Fact]
    public async Task Target_change_during_inspection_cannot_apply_a_different_executable()
    {
        using var fixture = new OptimizationFixture(); var native = new DelayedNative(fixture.Service());
        var first = Path.Combine(fixture.Temp, "first.exe"); var second = Path.Combine(fixture.Temp, "second.exe"); File.WriteAllText(first, "test"); File.WriteAllText(second, "test");
        var pc = new PcOptimizationService(native, () => false); pc.SetApplicationTarget(first); await pc.PreviewAsync();
        var applying = pc.ApplyAsync(["application-gpu"]); await native.Started.Task;
        pc.SetApplicationTarget(second); native.Release.TrySetResult(); await applying;
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, first).Exists);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GpuPreferences, second).Exists);
    }
    private sealed class DelayedNative(INativeOptimizationService inner) : INativeOptimizationService
    {
        private int calls; public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<OptimizationOverview> InspectAsync(CancellationToken token = default) { if (++calls == 2) { Started.TrySetResult(); await Release.Task.WaitAsync(token); } return await inner.InspectAsync(token); }
        public Task<OptimizationOutcome> SetApplicationGpuPreferenceAsync(string path, CancellationToken token = default) => inner.SetApplicationGpuPreferenceAsync(path, token);
        public Task<OptimizationOutcome> SetToggleAsync(string id, bool enabled, CancellationToken token = default) => inner.SetToggleAsync(id, enabled, token);
        public Task<OptimizationOutcome> SetPowerSchemeAsync(string id, CancellationToken token = default) => inner.SetPowerSchemeAsync(id, token);
        public Task<OptimizationOutcome> SetStartupEnabledAsync(string id, bool enabled, CancellationToken token = default) => inner.SetStartupEnabledAsync(id, enabled, token);
        public Task<OptimizationOutcome> UndoAsync(string id, CancellationToken token = default) => inner.UndoAsync(id, token);
        public Task<TemporaryCleanupPreview> PreviewTempAsync(CancellationToken token = default) => inner.PreviewTempAsync(token);
        public Task<TemporaryCleanupResult> CleanTempAsync(IReadOnlyCollection<string> ids, CancellationToken token = default) => inner.CleanTempAsync(ids, token);
    }
}
