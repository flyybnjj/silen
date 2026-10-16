using System.Globalization;
using System.Text;
using Silen.Gaming;
using Xunit;

namespace Silen.Gaming.Tests;

public sealed class PresentMonCsvTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Silen-GamingTests-" + Guid.NewGuid().ToString("N"));

    public PresentMonCsvTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void ParsesOfficialLegacyFixtureWithoutInventingLatency()
    {
        var session = PresentMonCsv.Parse(Path.Combine(AppContext.BaseDirectory, "Fixtures", "presentmon-2.3.1-gold-excerpt.csv"));
        Assert.Equal("dwm.exe", session.ProcessName);
        Assert.Equal("msBetweenPresents", session.TimingColumn);
        Assert.Equal(3, session.FrameCount);
        Assert.Equal(0, session.IgnoredRows);
        Assert.Equal(50.08346666666667, session.AverageFrameTimeMs, 8);
        Assert.Equal(19.9666689739262, session.AverageFps, 7);
        Assert.Equal(100.3707, session.P95FrameTimeMs);
        Assert.Equal(100.3707, session.P99FrameTimeMs);
        Assert.Equal(9.96306691096107, session.OnePercentLowFps, 7);
        Assert.Equal(new[] { 16.4754, 33.4043, 100.3707 }, session.FrameTimesMs);
    }

    [Fact]
    public void ParsesBomQuotedFieldsAndDecimalCommaIndependentlyOfCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var path = Write("Application;ProcessID;FrameTime\n\"juego;\"\"prueba\"\".exe\";12;\"12,5\"\n\"juego;\"\"prueba\"\".exe\";12;\"17,5\"\n", bom: true);
            var session = PresentMonCsv.Parse(path);
            Assert.Equal("juego;\"prueba\".exe", session.ProcessName);
            Assert.Equal("FrameTime", session.TimingColumn);
            Assert.Equal(15, session.AverageFrameTimeMs);
            Assert.Equal(66.66666666666667, session.AverageFps, 8);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void PrefersFrameTimeWhenBothTimingColumnsArePresent()
    {
        var session = PresentMonCsv.Parse(Write("Application,FrameTime,MsBetweenPresents\ngame.exe,20,100\n"));
        Assert.Equal("FrameTime", session.TimingColumn);
        Assert.Equal(50, session.AverageFps);
    }

    [Fact]
    public void PercentilesUseNearestRankAndLowFpsUsesSlowestCeilingOnePercent()
    {
        var rows = Enumerable.Range(1, 101).Select(i => "game.exe,32," + i.ToString(CultureInfo.InvariantCulture));
        var session = PresentMonCsv.Parse(Write("Application,ProcessID,MsBetweenPresents\n" + string.Join("\n", rows)));
        Assert.Equal(51, session.AverageFrameTimeMs);
        Assert.Equal(96, session.P95FrameTimeMs);
        Assert.Equal(100, session.P99FrameTimeMs);
        Assert.Equal(9.950248756218906, session.OnePercentLowFps, 8);
        Assert.Equal(1, session.FrameTimesMs[0]);
        Assert.Equal(101, session.FrameTimesMs[^1]);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e309")]
    [InlineData("NA")]
    [InlineData("")]
    public void DropsInvalidTimesAndCountsThem(string invalid)
    {
        var session = PresentMonCsv.Parse(Write($"Application,ProcessID,MsBetweenPresents\ngame.exe,32,10\ngame.exe,32,{invalid}\ngame.exe,32,20\n"));
        Assert.Equal(2, session.FrameCount);
        Assert.Equal(1, session.IgnoredRows);
        Assert.Equal(15, session.AverageFrameTimeMs);
    }

    [Fact]
    public void DropsRowsWithMissingOrExtraFields()
    {
        var session = PresentMonCsv.Parse(Write("Application,ProcessID,FrameTime\ngame.exe,32,10\ngame.exe,32\ngame.exe,32,5,unexpected\n"));
        Assert.Equal(1, session.FrameCount);
        Assert.Equal(2, session.IgnoredRows);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Application,FrameTime\n")]
    [InlineData("Application,FrameTime\ngame.exe,0\n")]
    [InlineData("Application,MsUntilDisplayed\ngame.exe,10\n")]
    [InlineData("Application,FrameTime,FrameTime\ngame.exe,10,20\n")]
    public void RejectsEmptyUnrecognizedOrAmbiguousData(string csv) => Assert.Throws<InvalidDataException>(() => PresentMonCsv.Parse(Write(csv)));

    [Theory]
    [InlineData("Application,ProcessID,FrameTime\ngame.exe,32,10\ngame.exe,33,20\n")]
    [InlineData("Application,FrameTime\ngame.exe,10\nother.exe,20\n")]
    [InlineData("Application,ProcessID,SwapChainAddress,FrameTime\ngame.exe,32,0x01,10\ngame.exe,32,0x02,20\n")]
    public void RejectsMixedProcessesOrSwapChainsInsteadOfMixingFps(string csv) => Assert.Throws<InvalidDataException>(() => PresentMonCsv.Parse(Write(csv)));

    [Fact]
    public void SupportsQuotedDecimalCommaInCommaSeparatedFiles()
    {
        var session = PresentMonCsv.Parse(Write("Application,ProcessID,FrameTime\n\"game, prueba.exe\",32,\"16,5\"\n"));
        Assert.Equal("game, prueba.exe", session.ProcessName);
        Assert.Equal(16.5, session.AverageFrameTimeMs);
    }

    [Fact]
    public void RejectsOversizedFilesBeforeLoadingFrames()
    {
        var path = Path.Combine(directory, "oversized.csv");
        using (var stream = File.Create(path)) stream.SetLength(64L * 1024 * 1024 + 1);
        Assert.Throws<InvalidDataException>(() => PresentMonCsv.Parse(path));
    }

    [Fact]
    public void DropsMalformedQuotedRecords()
    {
        var session = PresentMonCsv.Parse(Write("Application,ProcessID,FrameTime\ngame.exe,32,10\n\"broken\"unexpected,32,20\ngame.exe,32,30\n"));
        Assert.Equal(new[] { 10d, 30d }, session.FrameTimesMs);
        Assert.Equal(1, session.IgnoredRows);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("bad")]
    public void DropsInvalidProcessIds(string invalidPid)
    {
        var session = PresentMonCsv.Parse(Write($"Application,ProcessID,FrameTime\ngame.exe,32,10\ngame.exe,{invalidPid},20\n"));
        Assert.Equal(1, session.FrameCount);
        Assert.Equal(1, session.IgnoredRows);
    }

    [Fact]
    public void DoesNotOverflowMeanForFiniteFrameTimes()
    {
        var session = PresentMonCsv.Parse(Write("Application,FrameTime\ngame.exe,1e308\ngame.exe,1e308\n"));
        Assert.Equal(1e308, session.AverageFrameTimeMs);
        Assert.True(double.IsFinite(session.AverageFps));
        Assert.True(session.AverageFps > 0);
    }

    [Fact]
    public void RejectsTimesThatWouldProduceInfiniteFps() => Assert.Throws<InvalidDataException>(() => PresentMonCsv.Parse(Write("Application,FrameTime\ngame.exe,5e-324\n")));

    [Fact]
    public void RejectsFrameLimitBeforeRetainingMoreThanOneMillionFrames()
    {
        var path = Path.Combine(directory, "too-many-frames.csv");
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("Application,FrameTime");
            for (var i = 0; i < 1_000_001; i++) writer.WriteLine("game.exe,10");
        }
        Assert.Throws<InvalidDataException>(() => PresentMonCsv.Parse(path));
    }

    private string Write(string csv, bool bom = false)
    {
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllText(path, csv, new UTF8Encoding(bom));
        return path;
    }

    [Fact]
    public void PreCancelledImportDoesNotReadTheCsv()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PresentMonCsv.Parse("nonexistent.csv", cancellation.Token));
    }

    [Fact]
    public async Task CancellationInterruptsAnImportAlreadyReadingRows()
    {
        var path = Path.Combine(directory, "cancel-large.csv");
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("Application,ProcessID,FrameTime");
            for (var i = 0; i < 500_000; i++) writer.WriteLine("game.exe,32,16.6667");
        }
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var importing = Task.Run(() => { started.SetResult(); return PresentMonCsv.Parse(path, cancellation.Token); });
        await started.Task;
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => importing);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
