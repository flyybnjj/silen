using System.Diagnostics;
using Silen.Gaming;
using Xunit;

namespace Silen.Gaming.Tests;

public sealed class BundledPresentMonTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Silen bundled tests " + Guid.NewGuid().ToString("N"));
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon", "PresentMon.exe");

    [Fact]
    public void ResolvesVerifiedConsoleFromDirectoryWithSpaces()
    {
        var expected = CopyConsole(directory);

        Assert.Equal(expected, BundledPresentMon.Resolve(directory));
    }

    [Fact]
    public void ResolvesFromApplicationDirectoryByDefault()
    {
        Assert.Equal(Fixture, BundledPresentMon.Resolve());
    }

    [Fact]
    public void MissingBundledConsoleGivesRepairError()
    {
        Directory.CreateDirectory(directory);

        var error = Assert.Throws<InvalidOperationException>(() => BundledPresentMon.Resolve(directory));

        Assert.Contains("incluido", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Silen", error.Message);
    }

    [Fact]
    public void ModifiedConsoleIsRejectedBeforeItCanExecute()
    {
        var executable = CopyConsole(directory);
        using (var file = new FileStream(executable, FileMode.Open, FileAccess.ReadWrite))
        {
            file.Position = file.Length - 1;
            var original = file.ReadByte();
            file.Position = file.Length - 1;
            file.WriteByte((byte)(original ^ 0xFF));
        }

        var error = Assert.Throws<InvalidOperationException>(() => BundledPresentMon.Resolve(directory));

        Assert.Contains("dañado", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LockedConsoleGivesReadableVerificationError()
    {
        var executable = CopyConsole(directory);
        using var locked = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.None);

        var error = Assert.Throws<InvalidOperationException>(() => BundledPresentMon.Resolve(directory));

        Assert.IsAssignableFrom<IOException>(error.InnerException);
        Assert.Contains("comprobar", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IncludedNativeConsoleRunsHelpWithoutSdkOrExternalToolPath()
    {
        var start = new ProcessStartInfo
        {
            FileName = BundledPresentMon.Resolve(),
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        Directory.CreateDirectory(directory);
        start.Environment["PATH"] = Environment.GetFolderPath(Environment.SpecialFolder.System);
        start.Environment.Remove("DOTNET_ROOT");
        start.ArgumentList.Add("--help");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var help = (await output) + (await error);

            Assert.Equal(1, process.ExitCode);
            Assert.Contains("PresentMon 2.6.0", help);
            Assert.DoesNotContain("error:", help, StringComparison.OrdinalIgnoreCase);
            foreach (var argument in new[] { "--process_id", "--timed", "--terminate_after_timed", "--no_console_stats", "--v1_metrics", "--session_name", "--output_file", "--terminate_existing_session" })
                Assert.Contains(argument, help);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync();
            }
        }
    }

    private static string CopyConsole(string destination)
    {
        var executable = Path.Combine(destination, "tools", "PresentMon", "PresentMon.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.Copy(Fixture, executable);
        return executable;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
