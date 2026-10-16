using Silen.App;
using Xunit;

namespace Silen.Tests;

public class CaptureInputTests
{
    [Theory]
    [InlineData("abc", "30", "collector.exe")]
    [InlineData("0", "30", "collector.exe")]
    [InlineData("-1", "30", "collector.exe")]
    [InlineData("12", "14", "collector.exe")]
    [InlineData("12", "301", "collector.exe")]
    [InlineData("12", "30", "collector.ps1")]
    public void InvalidCaptureInputsReturnActionableError(string process, string duration, string executable)
    {
        var error = CaptureInputValidation.Validate(process, duration, executable, _ => true, out _, out _);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void MissingExecutableIsRejectedBeforeStartingCapture()
    {
        var error = CaptureInputValidation.Validate("12", "30", "missing.exe", _ => false, out _, out _);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("15", 15)]
    [InlineData("300", 300)]
    public void SupportedDurationAndExecutableAreAccepted(string duration, int expected)
    {
        var error = CaptureInputValidation.Validate("12", duration, "collector.EXE", _ => true, out var pid, out var seconds);
        Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.Equal(12, pid);
        Assert.Equal(expected, seconds);
    }
}
