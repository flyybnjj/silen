using System.Diagnostics;
using System.Text.Json;
using Silen.Core;
using Xunit;
using Xunit.Abstractions;

namespace Silen.Windows.Tests;

public sealed class ReadOnlyCollectionTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "ReadOnlyIntegration")]
    public async Task Actual_windows_scan_collects_ten_spaced_samples_without_exporting_profile_paths()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var elapsed = Stopwatch.StartNew();
        var progress = new List<ScanProgress>();
        var snapshot = await new WindowsCollector().CollectAsync(cancellationToken: cancellation.Token, detailedProgress: new ImmediateProgress<ScanProgress>(progress.Add));
        Assert.Equal(Enumerable.Range(1, 14), progress.Select(step => step.CompletedSteps).Distinct());
        Assert.All(progress, step => Assert.Equal(14, step.TotalSteps));
        // El inventario puede terminar antes o después de las primeras muestras.
        Assert.Null(progress.First().Sample);
        Assert.Same(snapshot.Machine, progress.First().Machine);
        var units = Assert.Single(progress, step => step.Drives is not null);
        Assert.Equal(snapshot.Drives, units.Drives);
        Assert.True(progress.IndexOf(units) < progress.FindIndex(step => step.CompletedSteps == 2));
        Assert.Equal(snapshot.Machine.DeviceKind == Silen.Core.DeviceKind.Laptop, snapshot.Machine.IsLaptop);
        Assert.Equal(snapshot.Samples, progress.Where(step => step.Sample is not null).Select(step => step.Sample));
        Assert.Equal(10, snapshot.Samples.Count);
        Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(9));
        Assert.Equal(10, snapshot.Samples.Select(s => s.At).Distinct().Count());
        Assert.All(snapshot.Samples, sample =>
        {
            Assert.True(sample.CpuPercent is null or >= 0 and <= 100);
            Assert.True(sample.MemoryPercent is null or >= 0 and <= 100);
        });
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.DoesNotContain(profile, string.Join('\n', snapshot.Startup.Select(s => s.Command)), StringComparison.OrdinalIgnoreCase);
        Assert.All(snapshot.Drivers, driver => Assert.True(driver.HardwareId.Count(character => character == '\\') <= 1));
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Samples = snapshot.Samples.Count, Drivers = snapshot.Drivers.Count, Drives = snapshot.Drives.Count,
            Startup = snapshot.Startup.Count, Processes = snapshot.Processes.Count, Networks = snapshot.Networks.Count,
            CpuReadings = snapshot.Samples.Count(s => s.CpuPercent is not null), MemoryReadings = snapshot.Samples.Count(s => s.MemoryPercent is not null),
            CpuTemperatureReadings = snapshot.Samples.Count(s => s.CpuTemperature is not null), GpuTemperatureReadings = snapshot.Samples.Count(s => s.GpuTemperature is not null),
            snapshot.Machine.Manufacturer, snapshot.Machine.Model, snapshot.Machine.ProductName, snapshot.Machine.SystemSku,
            snapshot.Machine.DeviceKind, snapshot.Machine.DeviceTypeEvidence, snapshot.Notes
        }));
    }

    private sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
