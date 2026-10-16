using Silen.App;
using Xunit;

namespace Silen.Tests;

public class OperationLifetimeTests
{
    [Fact]
    public async Task StopWaitsUntilTheCancelledOperationsCleanupActuallyFinishes()
    {
        var lifetime = new OperationLifetime();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaned = false;
        var work = lifetime.RunAsync(async token =>
        {
            try { entered.SetResult(); await Task.Delay(Timeout.Infinite, token); }
            finally { cleaning.SetResult(); await releaseCleanup.Task; cleaned = true; }
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stopping = lifetime.StopAsync();
        await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { Assert.False(stopping.IsCompleted); }
        finally { releaseCleanup.TrySetResult(); }
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.True(cleaned);
    }

    [Fact]
    public async Task StoppingPermanentlyPreventsNewWorkFromStarting()
    {
        var lifetime = new OperationLifetime();
        await lifetime.RunAsync(_ => Task.CompletedTask);
        await lifetime.StopAsync();
        Assert.True(lifetime.IsStopping);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lifetime.RunAsync(_ => Task.CompletedTask));
    }

    [Fact]
    public async Task CancellingAnOperationStillAllowsTheUserToRetry()
    {
        var lifetime = new OperationLifetime();
        var work = lifetime.RunAsync(token => Task.Delay(Timeout.Infinite, token));
        lifetime.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        var completed = false;
        await lifetime.RunAsync(_ => { completed = true; return Task.CompletedTask; });
        Assert.True(completed);
        Assert.False(lifetime.IsStopping);
    }

    [Fact]
    public async Task AFailedOperationDoesNotLeaveShutdownWaitingForever()
    {
        var lifetime = new OperationLifetime();
        await Assert.ThrowsAsync<IOException>(() => lifetime.RunAsync(_ => Task.FromException(new IOException("controlled failure"))));
        await lifetime.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(lifetime.IsStopping);
    }
}
