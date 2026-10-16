using Xunit;

namespace Silen.Windows.Tests;

public sealed class PcOptimizationTests
{
    [Fact]
    public async Task Preview_does_not_need_a_game_or_write_any_preferences()
    {
        using var fixture = new OptimizationFixture();
        var service = new PcOptimizationService(fixture.Service(), () => false);
        var preview = await service.PreviewAsync();
        Assert.Contains(preview.Options, option => option.Id == "game-mode" && option.Available);
        Assert.Contains(preview.Options, option => option.Id == "browser-background" && option.SelectedByDefault);
        Assert.All(preview.Options.Where(option => option.Id.StartsWith("service-") || option.Id == "power-throttling"), option => Assert.False(option.SelectedByDefault));
        Assert.False(preview.Options.Single(option => option.Id == "power-mode").Available);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Only_the_reviewed_selection_is_applied_and_repeat_is_idempotent()
    {
        using var fixture = new OptimizationFixture();
        var service = new PcOptimizationService(fixture.Service(), () => true);
        await service.PreviewAsync();
        var first = await service.ApplyAsync(["browser-background"]);
        Assert.Equal(1, first.Applied);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Exists);
        var second = await service.ApplyAsync(["browser-background"]);
        Assert.Equal(0, second.Applied);
        Assert.Equal(1, second.Unchanged);
        Assert.Single((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Unknown_or_duplicate_selections_do_not_partially_apply_a_valid_step()
    {
        using var fixture = new OptimizationFixture();
        var service = new PcOptimizationService(fixture.Service(), () => true);
        var unreviewed = await service.ApplyAsync(["game-mode"]);
        Assert.Equal(1, unreviewed.Failed);
        await service.PreviewAsync();
        Assert.Equal(1, (await service.ApplyAsync(["game-mode", "unknown"])).Failed);
        Assert.Equal(1, (await service.ApplyAsync(["game-mode", "game-mode"])).Failed);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Lost_permission_is_reported_as_omitted_while_other_selected_steps_complete()
    {
        using var fixture = new OptimizationFixture();
        var service = new PcOptimizationService(fixture.Service(), () => true);
        await service.PreviewAsync();
        fixture.Platform.DeniedWriteKeys.Add(@"Software\Policies\Microsoft\Edge");
        var result = await service.ApplyAsync(["game-mode", "browser-background"]);
        Assert.Equal(1, result.Applied);
        Assert.Equal(1, result.Skipped);
        Assert.Single((await fixture.Service().InspectAsync()).Changes);
    }

    [Fact]
    public async Task Cancellation_keeps_completed_changes_and_does_not_start_remaining_steps()
    {
        using var fixture = new OptimizationFixture();
        using var cancellation = new CancellationTokenSource();
        var service = new PcOptimizationService(fixture.Service(), () => true);
        await service.PreviewAsync();
        var progress = new ImmediateProgress(value => { if (value.Completed == 1) cancellation.Cancel(); });
        var result = await service.ApplyAsync(["game-mode", "browser-background"], progress, cancellation.Token);
        Assert.True(result.Cancelled);
        Assert.Equal(1, result.Applied);
        Assert.Single((await fixture.Service().InspectAsync()).Changes);
        Assert.False(fixture.Platform.ReadValue(@"Software\Policies\Microsoft\Edge", "StartupBoostEnabled").Exists);
    }

    [Fact]
    public async Task Battery_change_during_review_prevents_energy_mutation()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.Overlay = Guid.Empty;
        var ac = true;
        var service = new PcOptimizationService(fixture.Service(), () => ac);
        Assert.True((await service.PreviewAsync()).Options.Single(option => option.Id == "power-mode").Available);
        ac = false;
        var result = await service.ApplyAsync(["power-mode"]);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(Guid.Empty, fixture.Platform.Overlay);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }

    private sealed class ImmediateProgress(Action<PcOptimizationProgress> report) : IProgress<PcOptimizationProgress>
    {
        public void Report(PcOptimizationProgress value) => report(value);
    }
}
