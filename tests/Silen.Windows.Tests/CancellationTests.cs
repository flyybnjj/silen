using Xunit;

namespace Silen.Windows.Tests;

public sealed class CancellationTests
{
    [Fact]
    public async Task Cancelled_collector_does_not_start_hardware_acquisition()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new WindowsCollector().CollectAsync(cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Cancelled_driver_lookup_does_not_start_a_network_search()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DriverUpdateService().FindUpdatesAsync(cancellation.Token));
    }
}
