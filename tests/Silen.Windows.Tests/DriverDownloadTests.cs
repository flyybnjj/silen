using Silen.Core;
using Silen.Windows;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class DriverDownloadTests
{
    [Theory]
    [InlineData("", 1)]
    [InlineData("not-a-windows-update-id", 1)]
    [InlineData("12345678-1234-1234-1234-123456789abc", 0)]
    public async Task Invalid_identity_or_revision_is_rejected_without_starting_Windows_Update(string id, int revision)
    {
        var result = await new DriverDownloadService().DownloadAsync([new DriverUpdateInfo("Fixture", "", "", "", "", id, revision)]);
        Assert.False(result.Success);
        Assert.Empty(result.DownloadedIds);
    }

    [Fact]
    public async Task Duplicate_and_empty_requests_are_rejected_before_download()
    {
        var service = new DriverDownloadService();
        var offer = new DriverUpdateInfo("Fixture", "", "", "", "", "12345678-1234-1234-1234-123456789abc", 1);
        Assert.False((await service.DownloadAsync([])).Success);
        Assert.False((await service.DownloadAsync([offer, offer])).Success);
    }

    [Fact]
    public async Task Deadline_returns_while_cleanup_is_pending_and_prevents_an_overlapping_download()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new DriverDownloadService((_, token) =>
        {
            entered.TrySetResult();
            token.WaitHandle.WaitOne();
            releaseCleanup.Task.GetAwaiter().GetResult();
            return new(false, [], "Fixture cleanup finished");
        }, TimeSpan.FromMilliseconds(100));
        var offer = new DriverUpdateInfo("Fixture", "", "", "", "", "12345678-1234-1234-1234-123456789abc", 1);
        try
        {
            var pending = service.DownloadAsync([offer]);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False((await pending.WaitAsync(TimeSpan.FromSeconds(3))).Success);
            Assert.False(releaseCleanup.Task.IsCompleted);
            var overlapping = await service.DownloadAsync([offer]);
            Assert.False(overlapping.Success);
            Assert.Contains("anterior", overlapping.Message);
        }
        finally { releaseCleanup.TrySetResult(); }
    }
}
