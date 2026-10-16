using Silen.Windows;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class AppUpdateCacheTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SilenUpdateTests", Guid.NewGuid().ToString("N"));
    private string FileAt(string name, int hoursOld)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, name);
        File.WriteAllText(path, "fixture");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-hoursOld));
        return path;
    }
    private string Job(int hoursOld, string? unknown = null)
    {
        var path = Path.Combine(root, "jobs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "Silen.exe"), "fixture");
        File.WriteAllText(Path.Combine(path, "job.bin"), "fixture");
        if (unknown is not null) File.WriteAllText(Path.Combine(path, unknown), "keep");
        Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-hoursOld));
        return path;
    }
    [Fact]
    public void Retains_current_and_one_other_recent_zip_and_only_prunes_owned_stale_partials()
    {
        var current = FileAt(new string('1', 64) + ".zip", 100);
        var recent = FileAt(new string('2', 64) + ".zip", 1);
        var older = FileAt(new string('3', 64) + ".zip", 2);
        var stale = FileAt(Guid.NewGuid().ToString("N") + ".partial", 25);
        var active = FileAt(Guid.NewGuid().ToString("N") + ".partial", 1);
        var unknown = FileAt("personal.zip", 100);
        var unrelated = FileAt("personal.partial", 100);
        AppUpdateCache.Prune(root, current);
        Assert.True(File.Exists(current));
        Assert.True(File.Exists(recent));
        Assert.False(File.Exists(older));
        Assert.False(File.Exists(stale));
        Assert.True(File.Exists(active));
        Assert.True(File.Exists(unknown));
        Assert.True(File.Exists(unrelated));
    }
    [Fact]
    public void Retains_two_newest_helpers_and_preserves_unknown_job_contents()
    {
        var newest = Job(25);
        var second = Job(26);
        var obsolete = Job(27);
        var unknown = Job(0, "personal.txt");
        var arbitrary = Path.Combine(root, "jobs", "personal-folder");
        Directory.CreateDirectory(arbitrary);
        File.WriteAllText(Path.Combine(arbitrary, "job.bin"), "keep");
        AppUpdateCache.Prune(root, Path.Combine(root, new string('1', 64) + ".zip"));
        Assert.True(Directory.Exists(newest));
        Assert.True(Directory.Exists(second));
        Assert.False(Directory.Exists(obsolete));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(unknown, "personal.txt")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(arbitrary, "job.bin")));
    }
    [Fact]
    public void Locked_and_readonly_entries_do_not_block_other_cleanup_or_future_retry()
    {
        var current = FileAt(new string('1', 64) + ".zip", 0);
        FileAt(new string('2', 64) + ".zip", 1);
        var locked = FileAt(new string('3', 64) + ".zip", 2);
        var removable = FileAt(new string('4', 64) + ".zip", 3);
        var readOnly = FileAt(Guid.NewGuid().ToString("N") + ".partial", 100);
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        using (File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None)) AppUpdateCache.Prune(root, current);
        Assert.True(File.Exists(locked));
        Assert.True(File.Exists(readOnly));
        Assert.False(File.Exists(removable));
        File.SetAttributes(readOnly, FileAttributes.Normal);
        AppUpdateCache.Prune(root, current);
        Assert.False(File.Exists(locked));
        Assert.False(File.Exists(readOnly));
    }
    [Fact]
    public void Cleanup_uses_bounded_nonrecursive_enumeration()
    {
        for (var index = 0; index < 1100; index++) FileAt(Guid.NewGuid().ToString("N") + ".partial", 100);
        AppUpdateCache.Prune(root, Path.Combine(root, new string('1', 64) + ".zip"));
        Assert.True(Directory.GetFiles(root).Length >= 76);
    }
    [Fact]
    public void Missing_or_unsafe_cache_is_a_nonblocking_cleanup_failure()
    {
        AppUpdateCache.Prune(root, Path.Combine(root, "missing.zip"));
        AppUpdateCache.Prune("relative", "relative/file.zip");
        AppUpdateCache.Prune(Path.GetPathRoot(root)!, Path.Combine(root, "missing.zip"));
        Assert.False(Directory.Exists(root));
    }
    public void Dispose()
    {
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, true);
    }
}
