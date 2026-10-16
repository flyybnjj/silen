using System.Text.Json;
using Silen.Core;
using Xunit;

namespace Silen.Tests;

public sealed class AppDataPathsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SilenAppDataTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void FreshInstallationSelectsSilenWithoutCreatingDirectories()
    {
        Assert.Equal(Path.Combine(directory, "Silen"), AppDataPaths.ResolveRoot(directory));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task LegacyHistoryAndUndoBackupsStayAccessibleWithoutMovingOrChangingFiles()
    {
        var legacy = Path.Combine(directory, "FlyyOptimizer");
        var reports = Path.Combine(legacy, "reports");
        var optimization = Path.Combine(legacy, "optimization");
        var fortnite = Path.Combine(legacy, "fortnite");
        Directory.CreateDirectory(reports);
        Directory.CreateDirectory(optimization);
        Directory.CreateDirectory(fortnite);
        var snapshot = new SystemSnapshot { Machine = new() { CpuName = "Legacy CPU" } };
        var reportBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, ReportStore.JsonOptions);
        var reportPath = Path.Combine(reports, "legacy.json");
        await File.WriteAllBytesAsync(reportPath, reportBytes);
        var optimizationPath = Path.Combine(optimization, "optimizations.json");
        var fortnitePath = Path.Combine(fortnite, "fixture.original.bin");
        byte[] optimizationBytes = "legacy undo journal"u8.ToArray();
        byte[] fortniteBytes = [0, 255, 13, 10, 42];
        await File.WriteAllBytesAsync(optimizationPath, optimizationBytes);
        await File.WriteAllBytesAsync(fortnitePath, fortniteBytes);

        var root = AppDataPaths.ResolveRoot(directory);
        var history = await new ReportStore(Path.Combine(root, "reports")).ListAsync();

        Assert.Equal(legacy, root);
        Assert.Equal(reportPath, Assert.Single(history).Path);
        Assert.Equal(reportBytes, await File.ReadAllBytesAsync(reportPath));
        Assert.Equal(optimizationBytes, await File.ReadAllBytesAsync(Path.Combine(root, "optimization", "optimizations.json")));
        Assert.Equal(fortniteBytes, await File.ReadAllBytesAsync(Path.Combine(root, "fortnite", "fixture.original.bin")));
        Assert.Equal(3, Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length);
        Assert.False(Directory.Exists(Path.Combine(directory, "Silen")));
    }

    [Fact]
    public void LegacyRootWinsWhenBothRootsExist()
    {
        var legacy = Path.Combine(directory, "FlyyOptimizer");
        var modern = Path.Combine(directory, "Silen");
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(modern);
        File.WriteAllText(Path.Combine(modern, "preserve.txt"), "modern data");

        Assert.Equal(legacy, AppDataPaths.ResolveRoot(directory));
        Assert.Equal("modern data", File.ReadAllText(Path.Combine(modern, "preserve.txt")));
        Assert.True(Directory.Exists(legacy));
    }

    [Fact]
    public void ModernInstallationKeepsItsExistingRoot()
    {
        var modern = Path.Combine(directory, "Silen");
        Directory.CreateDirectory(modern);
        File.WriteAllText(Path.Combine(modern, "preserve.txt"), "modern data");

        Assert.Equal(modern, AppDataPaths.ResolveRoot(directory));
        Assert.Equal("modern data", File.ReadAllText(Path.Combine(modern, "preserve.txt")));
        Assert.False(Directory.Exists(Path.Combine(directory, "FlyyOptimizer")));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
