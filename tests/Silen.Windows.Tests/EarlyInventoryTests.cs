using Silen.Core;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class EarlyInventoryTests
{
    [Fact]
    public async Task Basic_identity_is_published_before_the_first_potentially_slow_WMI_query()
    {
        using var cancellation = new CancellationTokenSource();
        var progress = new List<ScanProgress>();
        var collector = new WindowsCollector((_, token) =>
        {
            Assert.NotEmpty(progress);
            var basic = progress[0];
            Assert.NotNull(basic.Machine);
            Assert.Equal(0, basic.CompletedSteps);
            Assert.Null(basic.Sample);
            Assert.True(basic.Machine.LogicalCores > 0);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return [];
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collector.CollectAsync(cancellationToken: cancellation.Token,
            detailedProgress: new Immediate(progress.Add)));
        Assert.DoesNotContain(progress, step => step.Sample is not null);
    }

    [Fact]
    public async Task Basics_are_published_before_driver_inventory_and_cancellation_returns_no_snapshot()
    {
        using var cancellation = new CancellationTokenSource();
        var progress = new List<ScanProgress>();
        var collector = new WindowsCollector((query, token) =>
        {
            if (query.Contains("Win32_PnPSignedDriver"))
            {
                Assert.Equal("Equipo de prueba", progress.Last(step => step.Machine is not null).Machine?.Manufacturer);
                Assert.Equal("C:", Assert.Single(progress.Last(step => step.Drives is not null).Drives!).Name);
                cancellation.Cancel(); token.ThrowIfCancellationRequested();
            }
            return query.Contains("Win32_ComputerSystem") && !query.Contains("Product")
                ? [new() { ["Manufacturer"] = "Equipo de prueba", ["Model"] = "Modelo" }]
                : query.Contains("Win32_LogicalDisk")
                    ? [new() { ["DeviceID"] = "C:", ["Size"] = 107374182400d, ["FreeSpace"] = 53687091200d }]
                    : [];
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collector.CollectAsync(cancellationToken: cancellation.Token,
            detailedProgress: new Immediate(progress.Add)));
        Assert.DoesNotContain(progress, step => step.Sample is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_or_denied_WMI_is_unknown_with_notes_while_native_samples_still_complete(bool denied)
    {
        var progress = new List<ScanProgress>();
        var collector = new WindowsCollector((_, _) => denied ? throw new UnauthorizedAccessException() : []);
        var result = await collector.CollectAsync(detailedProgress: new Immediate(progress.Add));
        Assert.Equal("", result.Machine.Manufacturer);
        Assert.Empty(result.Drives);
        Assert.Equal(10, result.Samples.Count);
        Assert.Contains(result.Notes, note => note.StartsWith("Sistema:") && note.Contains(denied ? "UnauthorizedAccessException" : "no entregó registros"));
        Assert.NotNull(progress.First().Machine);
        Assert.Empty(progress.Single(step => step.Drives is not null).Drives!);
    }

    private sealed class Immediate(Action<ScanProgress> report) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) => report(value);
    }
}
