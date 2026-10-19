using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Silen.Windows;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class AppUpdateJobTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SilenUpdateTests", Guid.NewGuid().ToString("N"));
    private string Cache => Path.Combine(root, "cache");
    private string Install => Path.Combine(root, "installed");
    private (ProcessStartInfo Start, AppRelease Release) CreateJob()
    {
        Directory.CreateDirectory(Install);
        Directory.CreateDirectory(Cache);
        File.WriteAllBytes(Path.Combine(Install, "Silen.exe"), "MZold fixture"u8.ToArray());
        var data = "archive fixture"u8.ToArray();
        var release = new AppRelease(new Version(1, 2, 0, 0), "v1.2.0", "https://github.com/flyybnjj/silen/releases/download/v1.2.0/Silen-win-x64.zip", data.Length, Convert.ToHexString(SHA256.HashData(data)));
        var path = Path.Combine(Cache, release.Sha256 + ".zip");
        File.WriteAllBytes(path, data);
        return (PortableAppUpdater.CreateStartInfo(new(release, path), Cache, Path.Combine(Install, "Silen.exe"), 123, 456, new Version(1, 1, 0)), release);
    }
    [Fact]
    public void Start_info_stages_own_exe_and_protected_job_without_starting()
    {
        var (start, _) = CreateJob();
        Assert.Equal("--apply-update", start.ArgumentList[0]);
        Assert.Equal(2, start.ArgumentList.Count);
        Assert.False(start.UseShellExecute);
        Assert.True(File.Exists(start.FileName));
        Assert.Equal(File.ReadAllBytes(Path.Combine(Install, "Silen.exe")), File.ReadAllBytes(start.FileName));
        var job = JsonSerializer.Deserialize<AppUpdateJob>(AppUpdateJobProtection.Unprotect(File.ReadAllBytes(start.ArgumentList[1])))!;
        Assert.Equal(Install, job.Installation);
        Assert.Equal(123, job.ParentId);
        Assert.Equal(456, job.ParentStartUtcTicks);
    }
    [Theory]
    [InlineData("tamper")]
    [InlineData("oversize")]
    [InlineData("wrong-helper")]
    [InlineData("outside-cache")]
    [InlineData("changed-install")]
    public async Task Untrusted_job_never_reaches_parent_wait_or_installer(string kind)
    {
        var (start, release) = CreateJob();
        var job = start.ArgumentList[1];
        if (kind == "tamper") { var bytes = File.ReadAllBytes(job); bytes[^20] ^= 1; File.WriteAllBytes(job, bytes); }
        if (kind == "oversize") File.WriteAllBytes(job, new byte[32769]);
        if (kind == "changed-install") File.WriteAllText(Path.Combine(Install, "Silen.exe"), "changed");
        if (kind == "outside-cache") { var target = Path.Combine(root, "job.bin"); File.Copy(job, target); job = target; }
        var helper = kind == "wrong-helper" ? Path.Combine(root, "other.exe") : start.FileName;
        await Assert.ThrowsAsync<InvalidDataException>(() => PortableAppUpdater.ApplyJobAsync(job, helper, Cache,
            (_, _) => Task.FromResult(release), (_, _) => throw new Xunit.Sdk.XunitException("untrusted parent wait"), _ => throw new Xunit.Sdk.XunitException("untrusted restart"), default));
    }
    [Fact]
    public async Task Changed_exact_tag_metadata_rejects_job_before_parent_wait()
    {
        var (start, release) = CreateJob();
        await Assert.ThrowsAsync<InvalidDataException>(() => PortableAppUpdater.ApplyJobAsync(start.ArgumentList[1], start.FileName, Cache,
            (tag, _) => Task.FromResult(tag == "v1.2.0" ? release with { Sha256 = new string('0', 64) } : release),
            (_, _) => throw new Xunit.Sdk.XunitException("parent wait"), _ => Task.CompletedTask, default));
    }
    [Fact]
    public async Task Reused_parent_pid_is_rejected()
    {
        using var current = Process.GetCurrentProcess();
        var job = new AppUpdateJob("unused", "unused", null!, current.Id, current.StartTime.ToUniversalTime().Ticks - 1, "unused", "1.1.0", DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<InvalidDataException>(() => PortableAppUpdater.WaitForParentAsync(job, default));
    }
    [Fact]
    public void Elevated_updates_are_refused_with_reopen_guidance()
    {
        var error = Assert.Throws<InvalidOperationException>(() => PortableAppUpdater.RequireUnelevated(true));
        Assert.Contains("administrador", error.Message);
        PortableAppUpdater.RequireUnelevated(false);
    }
    [Theory]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("root-install")]
    [InlineData("outside-archive")]
    [InlineData("downgrade")]
    [InlineData("helper-hash")]
    public async Task Invalid_protected_job_fields_are_rejected_before_network_or_wait(string kind)
    {
        var (start, _) = CreateJob();
        var path = start.ArgumentList[1];
        var job = JsonSerializer.Deserialize<AppUpdateJob>(AppUpdateJobProtection.Unprotect(File.ReadAllBytes(path)))!;
        job = kind switch
        {
            "expired" => job with { CreatedUtc = DateTimeOffset.UtcNow.AddHours(-3) },
            "future" => job with { CreatedUtc = DateTimeOffset.UtcNow.AddHours(3) },
            "root-install" => job with { Installation = Path.GetPathRoot(root)! },
            "outside-archive" => job with { ArchivePath = Path.Combine(root, "outside.zip") },
            "downgrade" => job with { InstalledVersion = "99.0.0" },
            _ => job with { OriginalSha256 = new string('0', 64) }
        };
        File.WriteAllBytes(path, AppUpdateJobProtection.Protect(JsonSerializer.SerializeToUtf8Bytes(job)));
        await Assert.ThrowsAsync<InvalidDataException>(() => PortableAppUpdater.ApplyJobAsync(path, start.FileName, Cache,
            (_, _) => throw new Xunit.Sdk.XunitException("network"), (_, _) => throw new Xunit.Sdk.XunitException("wait"), _ => Task.CompletedTask, default));
    }
    [Fact]
    public async Task Protected_job_revalidates_waits_applies_and_restarts_inside_sandbox()
    {
        Directory.CreateDirectory(Install);
        Directory.CreateDirectory(Cache);
        File.WriteAllBytes(Path.Combine(Install, "Silen.exe"), "MZold fixture"u8.ToArray());
        var source = Environment.ProcessPath!;
        var info = FileVersionInfo.GetVersionInfo(source);
        var version = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        var temporary = Path.Combine(root, "package.zip");
        using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(source, "Silen-win-x64/Silen.exe");
            using var resource = zip.CreateEntry("Silen-win-x64/Archivos/fixture.txt").Open();
            resource.Write("resource fixture"u8);
        }
        var data = File.ReadAllBytes(temporary);
        var tag = "v" + version;
        var release = new AppRelease(version, tag, $"https://github.com/flyybnjj/silen/releases/download/{tag}/Silen-win-x64.zip", data.Length, Convert.ToHexString(SHA256.HashData(data)));
        var archive = Path.Combine(Cache, release.Sha256 + ".zip");
        File.Move(temporary, archive);
        var start = PortableAppUpdater.CreateStartInfo(new(release, archive), Cache, Path.Combine(Install, "Silen.exe"), 123, 456, new Version(0, 1, 0));
        var waited = false;
        var restarted = false;
        await PortableAppUpdater.ApplyJobAsync(start.ArgumentList[1], start.FileName, Cache,
            (requestedTag, _) => Task.FromResult(requestedTag == tag ? release : throw new Xunit.Sdk.XunitException("wrong tag")),
            (_, _) => { waited = true; Assert.Equal("MZold fixture"u8.ToArray(), File.ReadAllBytes(Path.Combine(Install, "Silen.exe"))); return Task.CompletedTask; },
            executable => { Assert.True(waited); restarted = true; Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(executable)); return Task.CompletedTask; }, default);
        Assert.True(restarted);
        Assert.Equal("resource fixture", File.ReadAllText(Path.Combine(Install, "Archivos", "fixture.txt")));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
