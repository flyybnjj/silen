using Xunit;

namespace Silen.Windows.Tests;

public sealed class TemporaryCleanupProgressTests
{
    [Fact]
    public async Task Cleanup_reports_each_distinct_selection_and_recoverable_bytes_without_exposing_paths()
    {
        using var fixture = new OptimizationFixture();
        CreateOldFile(fixture.Temp, "privado.tmp", "1234");
        var service = fixture.Service();
        var id = Assert.Single((await service.PreviewTempAsync()).Files).Id;
        var progress = new RecordedProgress();

        var result = await service.CleanTempAsync([id, id, "desconocido"], progress);

        Assert.Collection(progress.Items,
            item => Assert.Equal(new TemporaryCleanupProgress(0, 2, 0, 0, 0, "Temporales de tu usuario"), item),
            item => Assert.Equal(new TemporaryCleanupProgress(1, 2, 1, 0, 4, "Temporales de tu usuario"), item),
            item => Assert.Equal(new TemporaryCleanupProgress(2, 2, 1, 1, 4, "Temporales de tu usuario"), item));
        Assert.Equal(0, result.BytesFreed);
        AssertMatchesResult(progress.Items[^1], result);
        Assert.Single(await service.ListRecoveryAsync());
    }

    [Fact]
    public async Task Cleanup_reports_locked_and_changed_files_as_processed_skips()
    {
        using var fixture = new OptimizationFixture();
        var locked = CreateOldFile(fixture.Temp, "ocupado.tmp", "1234");
        var changed = CreateOldFile(fixture.Temp, "cambiado.tmp", "1234");
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        using var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        File.WriteAllText(changed, "cambio posterior");
        var progress = new RecordedProgress();

        var result = await service.CleanTempAsync(preview.Files.Select(item => item.Id).ToArray(), progress);

        Assert.Equal(new[] { 0, 1, 2 }, progress.Items.Select(item => item.Processed));
        Assert.Equal(new[] { 0, 1, 2 }, progress.Items.Select(item => item.Skipped));
        Assert.All(progress.Items, item => Assert.Equal(0, item.Removed));
        Assert.Equal(0, result.Removed);
        Assert.Equal(2, result.Skipped);
        AssertMatchesResult(progress.Items[^1], result);
        Assert.True(File.Exists(locked));
        Assert.True(File.Exists(changed));
    }

    [Fact]
    public async Task Cancellation_reports_partial_work_and_remaining_skips_without_claiming_completion()
    {
        using var fixture = new OptimizationFixture();
        CreateOldFile(fixture.Temp, "a.tmp", "1234");
        CreateOldFile(fixture.Temp, "b.tmp", "1234");
        CreateOldFile(fixture.Temp, "c.tmp", "1234");
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        using var cancellation = new CancellationTokenSource();
        var progress = new RecordedProgress(item =>
        {
            if (item.Processed == 1) cancellation.Cancel();
        });

        var result = await service.CleanTempAsync(preview.Files.Select(item => item.Id).ToArray(), progress, cancellation.Token);

        Assert.Equal(1, result.Removed);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(4, result.RecoverableBytes);
        Assert.Equal(0, result.BytesFreed);
        Assert.Equal(1, progress.Items[^1].Processed);
        Assert.Equal(3, progress.Items[^1].Total);
        Assert.All(progress.Items, item => Assert.True(item.Processed < item.Total));
        AssertMatchesResult(progress.Items[^1], result);
        Assert.Equal(2, Directory.EnumerateFiles(fixture.Temp).Count());
        Assert.Single(await service.ListRecoveryAsync());
    }

    [Fact]
    public async Task Empty_cleanup_reports_initial_zero_counts()
    {
        using var fixture = new OptimizationFixture();
        var progress = new RecordedProgress();

        var result = await fixture.Service().CleanTempAsync([], progress);

        Assert.Equal(new TemporaryCleanupProgress(0, 0, 0, 0, 0, "Temporales de tu usuario"), Assert.Single(progress.Items));
        AssertMatchesResult(progress.Items[^1], result);
    }

    [Fact]
    public async Task Cancellation_before_validation_keeps_unprocessed_files_and_reports_matching_partial_result()
    {
        using var fixture = new OptimizationFixture();
        CreateOldFile(fixture.Temp, "a.tmp", "1234");
        CreateOldFile(fixture.Temp, "b.tmp", "1234");
        using var cancellation = new CancellationTokenSource();
        var cleaning = false;
        var checks = 0;
        var service = new NativeOptimizationService(fixture.Platform, fixture.Journal, fixture.Temp, () =>
        {
            if (cleaning && ++checks == 2) cancellation.Cancel();
            return DateTimeOffset.UtcNow;
        });
        var preview = await service.PreviewTempAsync();
        var progress = new RecordedProgress();
        cleaning = true;

        var result = await service.CleanTempAsync(preview.Files.Select(item => item.Id).ToArray(), progress, cancellation.Token);

        Assert.Equal(1, result.Removed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, progress.Items[^1].Processed);
        Assert.Equal(2, progress.Items[^1].Total);
        AssertMatchesResult(progress.Items[^1], result);
        Assert.Single(Directory.EnumerateFiles(fixture.Temp));
    }

    [Fact]
    public async Task Failing_progress_observer_does_not_interrupt_recoverable_cleanup_or_hide_result()
    {
        using var fixture = new OptimizationFixture();
        CreateOldFile(fixture.Temp, "a.tmp", "1234");
        var service = fixture.Service();
        var id = Assert.Single((await service.PreviewTempAsync()).Files).Id;
        var progress = new RecordedProgress(_ => throw new InvalidOperationException("Observador cerrado."));

        var result = await service.CleanTempAsync([id], progress);

        Assert.Equal(new[] { 0, 1 }, progress.Items.Select(item => item.Processed));
        Assert.Equal(1, result.Removed);
        AssertMatchesResult(progress.Items[^1], result);
        Assert.Single(await service.ListRecoveryAsync());
    }

    [Fact]
    public async Task System_action_cleanup_reports_local_progress_without_requesting_elevation()
    {
        using var fixture = new OptimizationFixture();
        CreateOldFile(fixture.Temp, "local.tmp", "1234");
        var service = new SystemActionOptimizationService(fixture.Service(), (_, _) =>
            throw new InvalidOperationException("La limpieza no debe solicitar elevación."));
        var id = Assert.Single((await service.PreviewTempAsync()).Files).Id;
        var progress = new RecordedProgress();

        var result = await service.CleanTempAsync([id], progress);

        Assert.Equal(1, result.Removed);
        Assert.Equal(new[] { 0, 1 }, progress.Items.Select(item => item.Processed));
        AssertMatchesResult(progress.Items[^1], result);
    }

    private static string CreateOldFile(string root, string name, string content)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-48));
        return path;
    }

    private static void AssertMatchesResult(TemporaryCleanupProgress progress, TemporaryCleanupResult result)
    {
        Assert.Equal(result.Removed, progress.Removed);
        Assert.Equal(result.Skipped, progress.Skipped);
        Assert.Equal(result.RecoverableBytes, progress.RecoverableBytes);
    }

    private sealed class RecordedProgress(Action<TemporaryCleanupProgress>? onReport = null) : IProgress<TemporaryCleanupProgress>
    {
        internal List<TemporaryCleanupProgress> Items { get; } = [];
        public void Report(TemporaryCleanupProgress value)
        {
            Items.Add(value);
            onReport?.Invoke(value);
        }
    }
}
