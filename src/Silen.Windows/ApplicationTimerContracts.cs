namespace Silen.Windows;

public sealed record TimerResolutionState(uint Coarsest, uint Finest, uint Current)
{
    public double MinimumMilliseconds => Finest / 10000d;
    public double CurrentMilliseconds => Current / 10000d;
    public bool Valid => Finest > 0 && Coarsest >= Finest && Coarsest <= 10000000 && Current > 0;
}
public interface IApplicationTimerService : IAsyncDisposable
{
    string Status { get; }
    bool Ready { get; }
    IReadOnlyList<string> Profiles { get; }
    bool IsEnabled(string path);
    TimerResolutionState? Inspect();
    Task StartAsync(CancellationToken token = default);
    Task<OptimizationOutcome> SetEnabledAsync(string path, bool enabled, CancellationToken token = default);
}
public sealed class InactiveApplicationTimerService : IApplicationTimerService
{
    public string Status => "Temporizador automático no iniciado en esta sesión.";
    public bool Ready => false;
    public IReadOnlyList<string> Profiles => [];
    public bool IsEnabled(string path) => false;
    public TimerResolutionState? Inspect() => null;
    public Task StartAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task<OptimizationOutcome> SetEnabledAsync(string path, bool enabled, CancellationToken token = default) => Task.FromResult(new OptimizationOutcome(false, Status));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
internal interface IApplicationTimerPlatform
{
    TimerResolutionState Read();
    void Request(uint interval);
    void Release();
}
internal interface IApplicationProcessProbe { bool? IsRunning(string path); }
internal interface IApplicationTimerStore
{
    IDisposable AcquireOwner();
    Task<string[]> ReadAsync(CancellationToken token);
    Task WriteAsync(string[] paths, CancellationToken token);
}
