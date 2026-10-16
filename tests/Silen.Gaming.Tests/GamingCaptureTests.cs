using System.Diagnostics;
using Silen.Gaming;
using Xunit;

namespace Silen.Gaming.Tests;

public sealed class GamingCaptureTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Silen-CaptureTests-" + Guid.NewGuid().ToString("N"));
    private readonly string executable = Path.Combine(AppContext.BaseDirectory, "FakePresentMon", "FakePresentMon.exe");
    private readonly GamingCapture capture = new();

    public GamingCaptureTests()
    {
        Directory.CreateDirectory(directory);

        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        Environment.SetEnvironmentVariable("DOTNET_ROOT", Directory.GetParent(runtimeDirectory)!.Parent!.Parent!.FullName);
        Environment.SetEnvironmentVariable("SILEN_TEST_SESSION_DIRECTORY", directory);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(300)]
    public async Task CapturesAtDurationBoundariesAndPreservesCsvWithSpacesAndShellMetacharacters(int duration)
    {
        var output = Path.Combine(directory, "medición & prueba con espacios.csv");
        var session = await capture.CaptureAsync(executable, 4101, duration, output);
        Assert.Equal(output, session.SourceFile);
        Assert.Equal(2, session.FrameCount);
        Assert.Equal(15, session.AverageFrameTimeMs);
        Assert.True(File.Exists(output));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(-1, 15)]
    [InlineData(4101, 14)]
    [InlineData(4101, 301)]
    public async Task RejectsInvalidPidAndDurationWithoutLaunching(int pid, int duration)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => capture.CaptureAsync(executable, pid, duration, Path.Combine(directory, "invalid.csv")));
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task RejectsMissingExecutable()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => capture.CaptureAsync(Path.Combine(directory, "missing.exe"), 4101, 15, Path.Combine(directory, "capture.csv")));
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task NeverOverwritesExistingOutput()
    {
        var output = Path.Combine(directory, "capture.csv");
        File.WriteAllText(output, "keep this data");
        await Assert.ThrowsAsync<IOException>(() => capture.CaptureAsync(executable, 4101, 15, output));
        Assert.Equal("keep this data", File.ReadAllText(output));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task ProtectsDestinationCreatedDuringCapture()
    {
        var output = Path.Combine(directory, "capture.csv");
        await Assert.ThrowsAsync<IOException>(() => capture.CaptureAsync(executable, 4106, 15, output));
        Assert.Equal("created by another writer", File.ReadAllText(output));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task DrainsLargeCollectorOutputWithBoundedErrorMessage()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => capture.CaptureAsync(executable, 4107, 15, Path.Combine(directory, "capture.csv")));
        Assert.Contains("ETW", error.Message);
        Assert.True(error.Message.Length < 5000);
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Theory]
    [InlineData(".cmd")]
    [InlineData(".ps1")]
    public async Task RejectsShellScriptsAsCollectorExecutables(string extension)
    {
        var script = Path.Combine(directory, "collector" + extension);
        File.WriteAllText(script, "not launched");
        await Assert.ThrowsAsync<ArgumentException>(() => capture.CaptureAsync(script, 4101, 15, Path.Combine(directory, "capture.csv")));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task RequiresCsvOutputExtension()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => capture.CaptureAsync(executable, 4101, 15, Path.Combine(directory, "capture.exe")));
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task DoesNotCreateUnrequestedOutputDirectories()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => capture.CaptureAsync(executable, 4101, 15, Path.Combine(directory, "missing", "capture.csv")));
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task StopsHungCollectorAfterBoundedCaptureDeadline()
    {
        var watch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() => capture.CaptureAsync(executable, 4109, 15, Path.Combine(directory, "capture.csv")));
        Assert.InRange(watch.Elapsed.TotalSeconds, 44, 55);
        Assert.Empty(Directory.GetFiles(directory, "*.session"));
        var pidFile = Assert.Single(Directory.GetFiles(directory));
        var collectorPid = int.Parse(File.ReadAllText(pidFile));
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(collectorPid));
    }

    [Fact]
    public async Task ReportsFailedCollectorAndRemovesOnlyItsTemporaryOutput()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => capture.CaptureAsync(executable, 4102, 15, Path.Combine(directory, "capture.csv")));
        Assert.Contains("7", error.Message);
        Assert.Contains("ETW", error.Message);
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Theory]
    [InlineData(4104)]
    [InlineData(4105)]
    public async Task RejectsMissingOrEmptyResultsWithoutPublishingCsv(int pid)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => capture.CaptureAsync(executable, pid, 15, Path.Combine(directory, "capture.csv")));
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task PreCancelledCaptureCreatesNoFiles()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.CaptureAsync(executable, 4101, 15, Path.Combine(directory, "capture.csv"), cancellation.Token));
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task CancellationTerminatesOnlyTheLaunchedCollector()
    {
        using var cancellation = new CancellationTokenSource();
        var output = Path.Combine(directory, "capture.csv");
        var task = capture.CaptureAsync(executable, 4103, 15, output, cancellation.Token);
        var watch = Stopwatch.StartNew();
        string? pidFile = null;
        while (watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            pidFile = Directory.GetFiles(directory, "*.pid").SingleOrDefault();
            if (pidFile is not null) break;
            if (task.IsCompleted) await task;
            await Task.Delay(25);
        }
        try
        {
            Assert.NotNull(pidFile);
            var collectorPid = int.Parse(File.ReadAllText(pidFile));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.Throws<ArgumentException>(() => Process.GetProcessById(collectorPid));
            Assert.False(File.Exists(output));
            Assert.Empty(Directory.GetFiles(directory, "*.session"));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            cancellation.Cancel();
            try { await task; } catch (OperationCanceledException) { }
        }
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
