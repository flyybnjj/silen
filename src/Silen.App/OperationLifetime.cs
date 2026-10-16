namespace Silen.App;


public sealed class OperationLifetime
{
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Task completion = Task.CompletedTask;
    private bool stopping;

    public bool IsStopping { get { lock (gate) return stopping; } }
    public bool CanCancel { get { lock (gate) return cancellation is { IsCancellationRequested: false }; } }

    public Task RunAsync(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        CancellationTokenSource source;
        TaskCompletionSource finished;
        lock (gate)
        {
            if (stopping) throw new InvalidOperationException("La aplicación se está cerrando.");
            if (cancellation is not null) throw new InvalidOperationException("Ya hay una operación en curso.");
            source = new CancellationTokenSource();
            finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellation = source;
            completion = finished.Task;
        }
        return ExecuteAsync(operation, source, finished);
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationTokenSource source, TaskCompletionSource finished)
    {
        try { await operation(source.Token); }
        finally
        {
            lock (gate) { cancellation = null; source.Dispose(); }
            finished.TrySetResult();
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? source;
        lock (gate) source = cancellation;
        try { source?.Cancel(); }
        catch (ObjectDisposedException) {  }
    }

    public async Task StopAsync()
    {
        Task active;
        lock (gate) { stopping = true; active = completion; }
        Cancel();

        await active;
    }
}
