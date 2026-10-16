using Silen.Core;
using Xunit;

namespace Silen.Tests;

public sealed class ReportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SilenTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Reports_and_exports_never_persist_startup_arguments_or_mutate_live_snapshot()
    {
        var snapshot = new SystemSnapshot { Startup = [new("Launcher", "app.exe --token private-argument", "Run")] };
        var path = await new ReportStore(directory).SaveAsync(snapshot);
        Assert.DoesNotContain("private-argument", await File.ReadAllTextAsync(path));
        Assert.DoesNotContain("private-argument", ReportFormatter.ToMarkdown(snapshot, []));
        Assert.Contains("private-argument", snapshot.Startup[0].Command);
    }

    [Fact]
    public async Task SavedSnapshotRoundTripsActualMeasurementsAndUnknownSensors()
    {
        var store = new ReportStore(directory);
        var snapshot = new SystemSnapshot { Machine = new() { CpuName = "Test CPU" }, Samples = [new() { MemoryPercent = 42, CpuTemperature = null }] };
        var file = await store.SaveAsync(snapshot);
        var result = await store.LoadAsync(file);
        Assert.Equal(snapshot.Id, result.Id);
        Assert.Equal("Test CPU", result.Machine.CpuName);
        Assert.Equal(42d, result.Samples[0].MemoryPercent);
        Assert.Null(result.Samples[0].CpuTemperature);
    }

    [Fact]
    public async Task HistoryIsNewestFirstAndIgnoresDamagedReportWithoutLosingValidOnes()
    {
        var store = new ReportStore(directory);
        await store.SaveAsync(new() { CapturedAt = DateTimeOffset.Parse("2026-01-01T12:00:00Z"), Machine = new() { CpuName = "Old" } });
        await store.SaveAsync(new() { CapturedAt = DateTimeOffset.Parse("2026-02-01T12:00:00Z"), Machine = new() { CpuName = "New" } });
        await File.WriteAllTextAsync(Path.Combine(directory, "damaged.json"), "{invalid");
        var history = await store.ListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("New", history[0].CpuName);
        Assert.Equal("Old", history[1].CpuName);
    }

    [Fact]
    public async Task LoadingCorruptReportExplainsInvalidData()
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "damaged.json");
        await File.WriteAllTextAsync(file, "{invalid");
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReportStore(directory).LoadAsync(file));
    }

    [Fact]
    public async Task GuideHistoryRecordsOpeningRatherThanAssumingOptimizationWasApplied()
    {
        var store = new ReportStore(directory);
        var action = new ActionRecord(DateTimeOffset.Now, "startup-review", "Revisar inicio", "Guía abierta");
        await store.RecordActionAsync(action);
        var actions = await store.GetActionsAsync();
        Assert.Single(actions);
        Assert.Equal("Guía abierta", actions[0].Outcome);
        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task ConcurrentGuideRecordsAreNotLost()
    {
        var store = new ReportStore(directory);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => store.RecordActionAsync(new(DateTimeOffset.Now, $"action-{i}", "Guide", "Guía abierta"))));
        Assert.Equal(10, (await store.GetActionsAsync()).Count);
    }

    [Fact]
    public async Task ConcurrentInstancesDoNotOverwriteEachOthersGuideHistory()
    {
        var first = new ReportStore(directory);
        var second = new ReportStore(directory);
        await Task.WhenAll(Enumerable.Range(0, 40).Select(i => (i % 2 == 0 ? first : second)
            .RecordActionAsync(new(DateTimeOffset.Now, $"action-{i}", "Guide", "Guía abierta"))));
        var records = await first.GetActionsAsync();
        Assert.Equal(40, records.Count);
        Assert.Equal(40, records.Select(record => record.FindingId).Distinct().Count());
    }

    [Fact]
    public async Task RefreshingHistoryWhileOtherInstancesWritePreservesEveryRecord()
    {
        var first = new ReportStore(directory);
        var second = new ReportStore(directory);
        var reader = new ReportStore(directory);
        await first.RecordActionAsync(new(DateTimeOffset.Now, "seed", "Guide", "Guía abierta"));
        using var stopReading = new CancellationTokenSource();
        var refreshing = Task.Run(async () =>
        {
            while (!stopReading.IsCancellationRequested)
                await reader.GetActionsAsync();
        });
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 60).Select(i => (i % 2 == 0 ? first : second)
                .RecordActionAsync(new(DateTimeOffset.Now, $"action-{i}", "Guide", "Guía abierta"))));
        }
        finally { stopReading.Cancel(); await refreshing; }
        Assert.Equal(61, (await reader.GetActionsAsync()).Count);
    }

    [Fact]
    public async Task BrokenActionLogIsPreservedInsteadOfSilentlyOverwritten()
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "actions.json");
        await File.WriteAllTextAsync(file, "{invalid");
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReportStore(directory).RecordActionAsync(new(DateTimeOffset.Now, "one", "Guide", "Guía abierta")));
        Assert.Equal("{invalid", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task StructurallyDamagedReportCannotCrashDownstreamAnalysis()
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "null-row.json");
        await File.WriteAllTextAsync(file, "{\"Id\":\"aac1b6f7-294f-463d-b8d5-e36ad19b77ce\",\"CapturedAt\":\"2026-01-01T12:00:00Z\",\"Samples\":[null]}");
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReportStore(directory).LoadAsync(file));
    }

    [Fact]
    public async Task NullActionRecordIsReportedAsCorruptionAndPreserved()
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "actions.json");
        await File.WriteAllTextAsync(file, "[null]");
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReportStore(directory).RecordActionAsync(new(DateTimeOffset.Now, "one", "Guide", "Guía abierta")));
        Assert.Equal("[null]", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task UnknownPowerPlanInOneReportDoesNotBreakEntireHistory()
    {
        var store = new ReportStore(directory);
        await store.SaveAsync(new() { Machine = new() { CpuName = "Known", PowerPlanGuid = "" } });
        await store.SaveAsync(new() { Machine = new() { CpuName = "Partial", PowerPlanGuid = null! } });
        var history = await store.ListAsync();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, report => report.CpuName == "Known");
        Assert.Contains(history, report => report.CpuName == "Partial");
    }

    [Fact]
    public void MarkdownIncludesMeasuredEvidenceAndNamesUnknownMetrics()
    {
        var snapshot = new SystemSnapshot { Machine = new() { CpuName = "Test CPU" }, Samples = [new() { MemoryPercent = 42 }] };
        var report = ReportFormatter.ToMarkdown(snapshot, [new("one", "Space", "Review storage", "9 GB libres", Severity.Warning, "Storage")]);
        Assert.Contains("Test CPU", report);
        Assert.Contains("9 GB libres", report);
        Assert.Contains("No disponible", report);
        Assert.Contains("42", report);
    }

    [Fact]
    public void MarkdownEscapesDeviceNamesThatContainTableDelimiters()
    {
        var snapshot = new SystemSnapshot { Drivers = [new() { DeviceName = "GPU|name\nextra", Version = "1.0" }] };
        var report = ReportFormatter.ToMarkdown(snapshot, []);
        Assert.Contains("GPU\\|name extra", report);
    }

    [Fact]
    public void ExportExplainsDetectedDeviceAndOfficialModelIdentity()
    {
        var snapshot = new SystemSnapshot { Machine = new() { DeviceKind = DeviceKind.Laptop, Manufacturer = "LENOVO", Model = "83GS", ProductName = "LOQ 15IAX9", SystemSku = "LENOVO_MT_83GS" } };
        var report = ReportFormatter.ToMarkdown(snapshot, []);
        Assert.Contains("Portátil", report);
        Assert.Contains("LOQ 15IAX9", report);
        Assert.Contains("83GS", report);
        Assert.Contains("LENOVO_MT_83GS", report);
    }

    [Fact]
    public void ComparisonKeepsUnavailableMetricsUnknown()
    {
        var before = new SystemSnapshot { Samples = [new() { MemoryPercent = 80 }] };
        var after = new SystemSnapshot { Samples = [new() { MemoryPercent = 60 }] };
        var comparison = SnapshotComparison.Compare(before, after);
        Assert.Equal(-20d, comparison.MemoryPercentagePointChange);
        Assert.Null(comparison.CpuPercentagePointChange);
        Assert.Null(comparison.CpuTemperatureChange);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
