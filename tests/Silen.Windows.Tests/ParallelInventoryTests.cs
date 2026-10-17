using System.Collections.Concurrent;
using Silen.Core;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class ParallelInventoryTests
{
    [Fact]
    public async Task Observation_starts_while_driver_inventory_is_blocked_and_cancellation_joins_the_worker()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sample = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = false;
        var collector = new WindowsCollector((query, token) =>
        {
            if (query.Contains("Win32_PnPSignedDriver"))
            {
                entered.TrySetResult();
                try { release.Wait(token); }
                finally { exited = true; }
            }
            return [];
        });
        var scan = collector.CollectAsync(cancellationToken: cancellation.Token,
            detailedProgress: new Immediate(step => { if (step.Sample is not null) sample.TrySetResult(); }));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(sample.Task, await Task.WhenAny(sample.Task, Task.Delay(TimeSpan.FromSeconds(5))));
            Assert.False(scan.IsCompleted);
        }
        finally
        {
            cancellation.Cancel(); release.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scan);
        }
        Assert.True(exited);
    }

    [Fact]
    public async Task Every_provider_receives_a_global_cancellable_budget_even_without_user_cancellation()
    {
        var seen = false;
        var collector = new WindowsCollector((_, token) =>
        {
            Assert.True(token.CanBeCanceled, "Las consultas necesitan un presupuesto global, también sin token del usuario.");
            seen = true;
            throw new OperationCanceledException(token);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collector.CollectAsync());
        Assert.True(seen);
    }

    [Fact]
    public async Task Expired_budget_is_explicit_and_waits_for_inventory_cleanup_without_returning_partial_data()
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = false;
        var collector = new WindowsCollector((query, token) =>
        {
            if (query.Contains("Win32_PnPSignedDriver"))
            {
                entered.TrySetResult();
                try { token.WaitHandle.WaitOne(); token.ThrowIfCancellationRequested(); }
                finally { Thread.Sleep(30); exited = true; }
            }
            return [];
        }, TimeSpan.FromSeconds(1));
        var scan = collector.CollectAsync(cancellationToken: cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var error = await Assert.ThrowsAsync<TimeoutException>(() => scan.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Contains("presupuesto de tiempo", error.Message);
            Assert.True(exited);
        }
        finally
        {
            cancellation.Cancel();
            try { await scan; } catch (Exception) { }
        }
    }

    [Fact]
    public async Task Inventory_failures_are_isolated_and_progress_stays_serial_with_all_ten_real_sample_times()
    {
        var progress = new ConcurrentQueue<ScanProgress>();
        var callbacks = 0;
        var peakCallbacks = 0;
        var collector = new WindowsCollector((query, _) => query.Contains("Win32_PnPSignedDriver")
            ? throw new UnauthorizedAccessException()
            : query.Contains("Win32_PnPEntity") ? throw new System.Runtime.InteropServices.COMException() : []);
        var result = await collector.CollectAsync(detailedProgress: new Immediate(step =>
        {
            var active = Interlocked.Increment(ref callbacks);
            Interlocked.Exchange(ref peakCallbacks, Math.Max(peakCallbacks, active));
            try { Thread.Sleep(1); progress.Enqueue(step); }
            finally { Interlocked.Decrement(ref callbacks); }
        }));
        Assert.Equal(1, peakCallbacks);
        Assert.Equal(Enumerable.Range(0, 15), progress.Select(step => step.CompletedSteps).Distinct());
        Assert.Contains(result.Notes, note => note.StartsWith("Drivers:") && note.Contains("UnauthorizedAccessException"));
        Assert.Contains(result.Notes, note => note.StartsWith("Dispositivos PnP:") && note.Contains("COMException"));
        Assert.Equal(10, result.Samples.Count);
        Assert.Equal(result.Samples, progress.Where(step => step.Sample is not null).Select(step => step.Sample));
        Assert.All(result.Samples.Zip(result.Samples.Skip(1)), pair => Assert.True(pair.Second.At - pair.First.At >= TimeSpan.FromMilliseconds(900)));
    }

    private sealed class Immediate(Action<ScanProgress> report) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) => report(value);
    }
}
