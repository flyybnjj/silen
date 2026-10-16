using Xunit;
namespace Silen.Windows.Tests;
public sealed class SystemActionProxyTests
{
    [Fact]
    public async Task Inspection_and_user_changes_never_open_an_elevated_session()
    {
        using var fixture = new OptimizationFixture();
        var calls = new List<SystemActionRequest>();
        var proxy = new SystemActionOptimizationService(fixture.Service(), (request, _) =>
        { calls.Add(request); return Task.FromResult(new SystemActionResponse(new(true, "Fixture"))); });
        await proxy.InspectAsync();
        Assert.True((await proxy.SetToggleAsync("game-mode", true)).Success);
        var user = Assert.Single((await proxy.InspectAsync()).Changes);
        Assert.True((await proxy.UndoAsync(user.Id)).Success);
        Assert.Empty(calls);
        Assert.Null((await proxy.InspectAsync()).Toggles.Single(toggle => toggle.Id == "game-mode").Enabled);
    }

    [Fact]
    public async Task Machine_actions_and_immutable_system_undo_ids_only_use_the_typed_executor()
    {
        using var fixture = new OptimizationFixture();
        var id = Guid.NewGuid().ToString("N");
        var calls = new List<SystemActionRequest>();
        var overview = new OptimizationOverview([], [], [], [new(id, "Fixture sistema", DateTimeOffset.UtcNow, true, "Protegido")]);
        var proxy = new SystemActionOptimizationService(fixture.Service(), (request, _) =>
        { calls.Add(request); return Task.FromResult(new SystemActionResponse(new(true, "Fixture"), overview)); });
        Assert.True((await proxy.SetToggleAsync("service-fax", true)).Success);
        var system = Assert.Single((await proxy.InspectAsync()).Changes);
        Assert.Equal("system:" + id, system.Id);
        Assert.True((await proxy.UndoAsync(system.Id)).Success);
        Assert.Equal(new(SystemActionKind.SetToggle, "service-fax", true), calls[0]);
        Assert.Equal(new(SystemActionKind.Undo, id), calls[1]);
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
        await Assert.ThrowsAsync<ArgumentException>(() => proxy.UndoAsync("system:../../arbitrary"));
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task Malformed_helper_invocation_exits_without_constructing_an_executor()
    {
        Assert.NotEqual(0, await SystemActionHost.RunAsync([SystemActionHost.Argument, "bad", "1", "bad"]));
        Assert.NotEqual(0, await SystemActionHost.RunAsync([SystemActionHost.Argument]));
    }
    [Fact]
    public async Task Closing_the_request_channel_cancels_cooperatively_and_preserves_the_existing_journal()
    {
        using var fixture = new OptimizationFixture();
        var session = Guid.NewGuid().ToString("N");
        using var ui = SystemActionHost.CreateSessionPipe(session);
        using var helper = new System.IO.Pipes.NamedPipeClientStream(".", "Silen.System." + session, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
        var connected = ui.WaitForConnectionAsync();
        await helper.ConnectAsync(); await connected;
        await ui.WriteAsync(new byte[] { 42 });
        Assert.Equal(42, helper.ReadByte()); // Solicitud ya recibida: ahora comienza la operación.
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = SystemActionHost.ExecuteWhileConnectedAsync(helper, async token =>
        {
            Assert.True((await fixture.Service().SetToggleAsync("game-mode", true, token)).Success);
            entered.SetResult();
            await Task.Delay(300, token);
            return await fixture.Service().SetToggleAsync("xbox-captures", false, token);
        }, CancellationToken.None);
        await entered.Task;
        ui.Dispose(); // Equivalente a cancelar/cerrar la UI; ningún Kill privilegiado es necesario.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
        var history = await fixture.Service().InspectAsync();
        var prior = Assert.Single(history.Changes);
        Assert.True(prior.CanUndo);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameDvr, "AppCaptureEnabled").Exists);
        Assert.True((await fixture.Service().UndoAsync(prior.Id)).Success);
    }
}
