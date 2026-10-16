using Xunit;

namespace Silen.Windows.Tests;

public sealed class ApplicationTimerTests
{
    [Fact]
    public async Task Main_optimizer_offers_manual_target_timer_and_input_without_enabling_them()
    {
        using var fixture = new OptimizationFixture(); var path = Executable(fixture);
        await using var timers = new ApplicationTimerService(new TimerPlatform(), new ProcessProbe(), new TimerStore());
        await timers.StartAsync();
        IPcOptimizationService pc = new PcOptimizationService(fixture.Service()); pc.AttachTimerService(timers);
        pc.SetApplicationTarget(path);
        var preview = await pc.PreviewAsync();
        var timer = preview.Options.Single(option => option.Id == "application-timer");
        Assert.True(timer.Available); Assert.False(timer.SelectedByDefault); Assert.False(timers.IsEnabled(path));
        Assert.Contains(preview.Options, option => option.Id == "input-filterkeys" && !option.SelectedByDefault);
        Assert.Equal(1, (await pc.ApplyAsync(["application-timer"])).Applied);
        Assert.True(timers.IsEnabled(path)); Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }
    [Fact]
    public async Task Timer_is_owned_only_during_the_exact_selected_process_and_released_on_exit()
    {
        using var fixture = new OptimizationFixture(); var path = Executable(fixture);
        var timer = new TimerPlatform(); var process = new ProcessProbe(); var store = new TimerStore { Paths = [path] };
        await using var service = new ApplicationTimerService(timer, process, store);
        await service.StartAsync(); Assert.Equal(0, timer.Requests);
        process.Running.Add(path); await service.TickAsync();
        Assert.Equal(1, timer.Requests); Assert.Equal(5000u, timer.Requested);
        await service.TickAsync(); Assert.Equal(1, timer.Requests);
        process.Running.Clear(); await service.TickAsync(); Assert.Equal(1, timer.Releases);
        Assert.Contains(path, process.CheckedPaths);
    }
    [Fact]
    public async Task An_external_minimum_does_not_replace_our_ownership_and_is_not_released()
    {
        using var fixture = new OptimizationFixture(); var path = Executable(fixture);
        var timer = new TimerPlatform { OtherOwner = true, State = new(156250, 5000, 5000) };
        var process = new ProcessProbe(); process.Running.Add(path);
        var service = new ApplicationTimerService(timer, process, new TimerStore { Paths = [path] });
        await service.StartAsync(); Assert.Equal(1, timer.Requests);
        await service.DisposeAsync(); Assert.Equal(1, timer.Releases); Assert.Equal(5000u, timer.State.Current);
    }
    [Fact]
    public async Task A_lost_request_is_reasserted_without_accumulating_or_resetting_foreign_requests()
    {
        using var fixture = new OptimizationFixture(); var path = Executable(fixture);
        var timer = new TimerPlatform(); var process = new ProcessProbe(); process.Running.Add(path);
        var service = new ApplicationTimerService(timer, process, new TimerStore { Paths = [path] });
        await service.StartAsync(); timer.State = timer.State with { Current = 10000 };
        await service.TickAsync(); Assert.Equal(2, timer.Requests);
        await service.DisposeAsync(); Assert.Equal(1, timer.Releases);
    }
    [Fact]
    public async Task One_configuration_is_persisted_and_can_be_removed_after_the_executable_is_deleted()
    {
        using var fixture = new OptimizationFixture(); var path = Executable(fixture);
        var store = new TimerStore(); var timer = new TimerPlatform(); var process = new ProcessProbe();
        var service = new ApplicationTimerService(timer, process, store);
        await service.StartAsync(); Assert.True((await service.SetEnabledAsync(path, true)).Success);
        await service.DisposeAsync();
        await using var restarted = new ApplicationTimerService(timer, process, store); await restarted.StartAsync();
        Assert.True(restarted.IsEnabled(path)); File.Delete(path);
        Assert.True((await restarted.SetEnabledAsync(path, false)).Success); Assert.Empty(store.Paths);
    }
    [Fact]
    public async Task Invalid_capabilities_and_unknown_process_do_not_activate_the_timer()
    {
        using var fixture = new OptimizationFixture(); var path = Executable(fixture);
        var timer = new TimerPlatform { State = new(156250, 0, 156250) }; var process = new ProcessProbe(); process.Running.Add(path);
        await using var service = new ApplicationTimerService(timer, process, new TimerStore { Paths = [path] });
        await service.StartAsync(); Assert.Equal(0, timer.Requests);
        Assert.Null(service.Inspect());
        timer.State = new(156250, 5000, 156250); process.Unknown = true;
        await service.TickAsync(); Assert.Equal(0, timer.Requests);
    }
    [Fact]
    public async Task A_second_controller_cannot_save_or_activate_profiles()
    {
        using var fixture = new OptimizationFixture(); var path = Executable(fixture);
        var store = new TimerStore { Locked = true }; var timer = new TimerPlatform();
        await using var service = new ApplicationTimerService(timer, new ProcessProbe(), store);
        await service.StartAsync(); Assert.False(service.Ready);
        Assert.False((await service.SetEnabledAsync(path, true)).Success); Assert.Empty(store.Paths); Assert.Equal(0, timer.Requests);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelling_start_releases_the_owner_and_allows_a_clean_retry(bool throwDuringRead)
    {
        using var cancellation = new CancellationTokenSource();
        var store = new TimerStore { OnRead = cancellation.Cancel, ThrowAfterRead = throwDuringRead };
        await using var service = new ApplicationTimerService(new TimerPlatform(), new ProcessProbe(), store);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StartAsync(cancellation.Token));
        Assert.False(service.Ready); Assert.Equal(1, store.ReleasedOwners);
        store.OnRead = null; store.ThrowAfterRead = false;
        await service.StartAsync(); Assert.True(service.Ready);
    }
    [Theory]
    [InlineData("[\"relative.exe\"]")]
    [InlineData("{ archivo dañado")]
    public async Task Invalid_saved_profiles_are_preserved_without_requests(string content)
    {
        using var fixture = new OptimizationFixture(); var root = Path.Combine(fixture.Temp, "profiles"); Directory.CreateDirectory(root);
        var file = Path.Combine(root, "profiles.json"); File.WriteAllText(file, content);
        var timer = new TimerPlatform();
        await using var service = new ApplicationTimerService(timer, new ProcessProbe(), new FileApplicationTimerStore(root));
        await service.StartAsync(); Assert.False(service.Ready); Assert.Equal(0, timer.Requests); Assert.Equal(content, File.ReadAllText(file));
    }
    private static string Executable(OptimizationFixture fixture)
    {
        var path = Path.Combine(fixture.Temp, "game.exe"); File.WriteAllText(path, "fixture; never execute"); return path;
    }
    internal sealed class TimerPlatform : IApplicationTimerPlatform
    {
        public TimerResolutionState State = new(156250, 5000, 156250);
        public int Requests, Releases; public uint Requested; public bool OtherOwner;
        public TimerResolutionState Read() => State;
        public void Request(uint interval) { Requests++; Requested = interval; State = State with { Current = interval }; }
        public void Release() { Releases++; State = State with { Current = OtherOwner ? State.Finest : State.Coarsest }; }
    }
    internal sealed class ProcessProbe : IApplicationProcessProbe
    {
        public HashSet<string> Running = new(StringComparer.OrdinalIgnoreCase); public List<string> CheckedPaths = []; public bool Unknown;
        public bool? IsRunning(string path) { CheckedPaths.Add(path); return Unknown ? null : Running.Contains(path); }
    }
    internal sealed class TimerStore : IApplicationTimerStore
    {
        public string[] Paths = []; public bool Locked;
        public Action? OnRead; public bool ThrowAfterRead; public int ReleasedOwners;
        public IDisposable AcquireOwner() => Locked ? throw new IOException("Otra sesión controla el temporizador.") : new Lease(() => ReleasedOwners++);
        public Task<string[]> ReadAsync(CancellationToken token) { OnRead?.Invoke(); if (ThrowAfterRead) token.ThrowIfCancellationRequested(); return Task.FromResult(Paths.ToArray()); }
        public Task WriteAsync(string[] paths, CancellationToken token) { Paths = paths.ToArray(); return Task.CompletedTask; }
        private sealed class Lease(Action release) : IDisposable { public void Dispose() => release(); }
    }
}
