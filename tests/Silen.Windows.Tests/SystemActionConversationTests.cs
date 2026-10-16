using System.Text.Json;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class SystemActionConversationTests
{
    [Fact]
    public async Task Local_named_pipe_peer_is_accepted_without_weakening_process_authentication()
    {
        var session = Guid.NewGuid().ToString("N");
        using var server = SystemActionHost.CreateSessionPipe(session);
        using var client = new System.IO.Pipes.NamedPipeClientStream(".", "Silen.System." + session,
            System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
        var connected = server.WaitForConnectionAsync();
        await client.ConnectAsync(1000); await connected;
        Assert.True(SystemActionHost.IsLocalPipeClient(server.SafePipeHandle));
    }
    [Fact]
    public async Task Operation_is_acknowledged_before_history_is_even_opened_and_survives_its_failure()
    {
        using var wire = new MemoryStream();
        var outcome = new OptimizationOutcome(true, "Aplicado y comprobado", true);
        await SystemActionConversation.SendAsync(wire, new(outcome), _ =>
        {
            Assert.InRange(wire.Length, 1, 4096);
            throw new IOException("historial inaccesible simulado");
        }, default);
        wire.Position = 0;
        var received = await SystemActionConversation.ReceiveAsync(wire, default);
        Assert.True(received.Outcome.Success);
        Assert.True(received.Outcome.Changed);
        Assert.Equal(ChangeVerification.Verified, received.Outcome.Verification);
        Assert.NotNull(received.HistoryWarning);
        Assert.Null(received.Overview);
    }

    [Fact]
    public async Task Large_history_is_paged_in_one_session_without_removing_backups()
    {
        using var fixture = new OptimizationFixture();
        var entries = Enumerable.Range(0, 1025).Select(index => new OptimizationJournalEntry
        {
            ToolId = "game-mode", Title = new string('x', 1500), AppliedAt = DateTimeOffset.UtcNow.AddMinutes(-index),
            Values = [new("Registry", OptimizationTargets.GameBar, "AllowAutoGameMode", RegistryValueState.Absent, RegistryValueState.Dword(1))]
        }).ToList();
        var journal = new OptimizationJournal(fixture.Journal, OptimizationAuthority.TestOnly);
        await using (await journal.LeaseAsync(default)) { await journal.WriteAsync(entries, default, addingEntry: true); }
        var path = Path.Combine(fixture.Journal, "optimizations.json");
        var before = await File.ReadAllBytesAsync(path);
        Assert.True(before.Length > SystemActionPolicy.MaximumResponseBytes);
        var pages = new List<SystemActionHistoryPage>();
        await foreach (var page in fixture.Service().ReadHistoryPagesAsync(default)) pages.Add(page);
        Assert.Equal(17, pages.Count);
        Assert.All(pages, page => Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(page, SystemActionPolicy.Json).Length, 1, SystemActionPolicy.MaximumResponseBytes));
        Assert.All(pages, page => Assert.InRange(page.Changes.Count, 1, SystemActionPolicy.HistoryPageSize));
        using var wire = new MemoryStream();
        await SystemActionConversation.SendAsync(wire, new(new(true, "Consulta autorizada")), fixture.Service().ReadHistoryPagesAsync, default);
        wire.Position = 0;
        var result = await SystemActionConversation.ReceiveAsync(wire, default);
        Assert.Null(result.HistoryWarning);
        Assert.Equal(entries.Select(entry => entry.Id), result.Overview!.Changes.Select(change => change.Id));
        Assert.All(result.Overview.Changes, change => Assert.Equal(256, change.Title.Length));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Equal(1500, (await journal.ReadAsync(default))[0].Title.Length);
    }

    [Fact]
    public async Task Incomplete_or_misordered_pages_do_not_replace_the_known_acknowledgement()
    {
        using var wire = new MemoryStream();
        await SystemActionHost.WriteMessageAsync(wire, JsonSerializer.SerializeToUtf8Bytes(
            new SystemActionResponse(new(true, "Aplicado", true), HistoryFollows: true), SystemActionPolicy.Json), SystemActionPolicy.MaximumResponseBytes, default);
        await SystemActionHost.WriteMessageAsync(wire, JsonSerializer.SerializeToUtf8Bytes(
            new SystemActionHistoryPage(2, [], false), SystemActionPolicy.Json), SystemActionPolicy.MaximumResponseBytes, default);
        wire.Position = 0;
        var result = await SystemActionConversation.ReceiveAsync(wire, default);
        Assert.True(result.Outcome.Success);
        Assert.True(result.Outcome.Changed);
        Assert.NotNull(result.HistoryWarning);
        Assert.Null(result.Overview);
    }

    [Fact]
    public async Task Missing_final_page_keeps_the_acknowledgement_and_rejects_partial_history()
    {
        using var wire = new MemoryStream();
        await SystemActionHost.WriteMessageAsync(wire, JsonSerializer.SerializeToUtf8Bytes(
            new SystemActionResponse(new(false, "Verificación incierta", Verification: ChangeVerification.Uncertain), HistoryFollows: true), SystemActionPolicy.Json), SystemActionPolicy.MaximumResponseBytes, default);
        wire.Position = 0;
        var result = await SystemActionConversation.ReceiveAsync(wire, default);
        Assert.Equal(ChangeVerification.Uncertain, result.Outcome.Verification);
        Assert.NotNull(result.HistoryWarning);
    }

    [Fact]
    public async Task Paging_holds_a_consistent_journal_lease_and_releases_it_when_cancelled()
    {
        using var fixture = new OptimizationFixture();
        await fixture.Service().SetToggleAsync("game-mode", true);
        await using (var pages = fixture.Service().ReadHistoryPagesAsync(default).GetAsyncEnumerator())
        {
            Assert.True(await pages.MoveNextAsync());
            using var cancel = new CancellationTokenSource();
            var blocked = fixture.Service().SetToggleAsync("xbox-captures", true, cancel.Token);
            await Task.Delay(80);
            Assert.False(blocked.IsCompleted);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked);
        }
        Assert.True((await fixture.Service().SetToggleAsync("xbox-captures", true)).Success);
    }
}
