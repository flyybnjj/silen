using Xunit;

namespace Silen.Windows.Tests;

public sealed class UncertainVerificationTests
{
    [Fact]
    public async Task Lost_read_access_after_a_write_is_uncertain_and_backup_remains_restorable()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.BeforeWrite = () => fixture.Platform.BeforeRead = () => throw new UnauthorizedAccessException("lectura simulada denegada");
        var outcome = await fixture.Service().SetToggleAsync("game-mode", true);
        Assert.False(outcome.Success);
        Assert.Contains("Verificación incierta", outcome.Message);
        Assert.Equal(ChangeVerification.Uncertain, outcome.Verification);
        Assert.False(outcome.Changed);
        Assert.True(outcome.MayHaveChanged);
        var response = new SystemActionResponse(outcome);
        Assert.Equal(outcome, System.Text.Json.JsonSerializer.Deserialize<SystemActionResponse>(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(response, SystemActionPolicy.Json), SystemActionPolicy.Json)!.Outcome);
        fixture.Platform.BeforeRead = null; fixture.Platform.BeforeWrite = null;
        var saved = Assert.Single((await fixture.Service().InspectAsync()).Changes);
        Assert.True(saved.CanUndo);
        Assert.True((await fixture.Service().UndoAsync(saved.Id)).Success);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Exists);
    }

    [Fact]
    public async Task Lost_read_access_during_undo_never_claims_that_the_result_was_verified()
    {
        using var fixture = new OptimizationFixture();
        await fixture.Service().SetToggleAsync("game-mode", true);
        var saved = Assert.Single((await fixture.Service().InspectAsync()).Changes);
        fixture.Platform.BeforeWrite = () => fixture.Platform.BeforeRead = () => throw new IOException("lectura simulada fallida");
        var outcome = await fixture.Service().UndoAsync(saved.Id);
        Assert.False(outcome.Success);
        Assert.Contains("Verificación incierta", outcome.Message);
        Assert.Equal(ChangeVerification.Uncertain, outcome.Verification);
        fixture.Platform.BeforeRead = null; fixture.Platform.BeforeWrite = null;
        Assert.True((await fixture.Service().UndoAsync(saved.Id)).Success);
    }

    [Fact]
    public async Task Batch_propagates_uncertainty_without_counting_it_as_unchanged_or_applied()
    {
        using var fixture = new OptimizationFixture();
        var batch = new PcOptimizationService(fixture.Service(), () => true);
        await batch.PreviewAsync();
        fixture.Platform.BeforeWrite = () => fixture.Platform.BeforeRead = () => throw new IOException("lectura simulada fallida");
        var result = await batch.ApplyAsync(["game-mode"]);
        var step = Assert.Single(result.Steps);
        Assert.Equal(ChangeVerification.Uncertain, step.Verification);
        Assert.Equal("Verificación incierta", step.State);
        Assert.True(step.MayHaveChanged);
        Assert.Equal(1, result.Uncertain);
        Assert.Equal(0, result.Applied);
        Assert.Equal(0, result.Unchanged);
        Assert.Contains("Verificación incierta", result.Message);
    }

    [Fact]
    public async Task Verified_write_denial_is_distinct_from_unknown_readback()
    {
        using var fixture = new OptimizationFixture();
        fixture.Platform.FailWriteNumber = 1;
        var outcome = await fixture.Service().SetToggleAsync("game-mode", true);
        Assert.False(outcome.Success);
        Assert.Equal(ChangeVerification.Verified, outcome.Verification);
        Assert.False(outcome.MayHaveChanged);
    }
}
