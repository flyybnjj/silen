using Xunit;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Silen.Windows.Tests;

public sealed class TemporaryCleanupTests
{
    [Fact]
    public async Task Preview_only_includes_old_files_under_owned_temp_without_deleting_anything()
    {
        using var fixture = new OptimizationFixture();
        var old = Create(fixture.Temp, "nested/old.log", "old", 48);
        var fresh = Create(fixture.Temp, "fresh.log", "fresh", 1);
        var outside = Create(fixture.Root, "outside.txt", "outside", 48);
        var preview = await fixture.Service().PreviewTempAsync();
        var item = Assert.Single(preview.Files);
        Assert.Equal(Path.Combine("nested", "old.log"), item.RelativePath);
        Assert.Equal(3, item.Bytes);
        Assert.Equal(3, preview.TotalBytes);
        Assert.True(File.Exists(old));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task Cleanup_retires_selected_files_without_claiming_freed_disk_space()
    {
        using var fixture = new OptimizationFixture();
        var selected = Create(fixture.Temp, "selected.tmp", "selected", 48);
        var kept = Create(fixture.Temp, "kept.tmp", "kept", 48);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        var id = preview.Files.Single(x => x.RelativePath == "selected.tmp").Id;
        var result = await service.CleanTempAsync([id, id]);
        Assert.Equal(1, result.Removed);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.BytesFreed);
        Assert.Equal(8, result.RecoverableBytes);
        Assert.NotNull(result.RecoveryId);
        Assert.False(File.Exists(selected));
        Assert.True(File.Exists(kept));
        Assert.True(Directory.Exists(fixture.Temp));
    }

    [Fact]
    public async Task Path_shaped_or_unknown_ids_cannot_delete_files_outside_preview()
    {
        using var fixture = new OptimizationFixture();
        var outside = Create(fixture.Root, "outside.txt", "keep", 48);
        var service = fixture.Service();
        await service.PreviewTempAsync();
        var result = await service.CleanTempAsync([outside, "../outside.txt", "unknown"]);
        Assert.Equal(0, result.Removed);
        Assert.Equal(3, result.Skipped);
        Assert.True(File.Exists(outside));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Changed_size_or_write_time_is_skipped(bool sizeChange)
    {
        using var fixture = new OptimizationFixture();
        var path = Create(fixture.Temp, "old.tmp", "1234", 48);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        if (sizeChange) { File.WriteAllText(path, "longer"); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-48)); }
        else File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-1));
        var result = await service.CleanTempAsync([Assert.Single(preview.Files).Id]);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Skipped);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task In_use_file_is_skipped_and_result_does_not_claim_reclaimed_bytes()
    {
        using var fixture = new OptimizationFixture();
        var path = Create(fixture.Temp, "locked.tmp", "locked", 48);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = await service.CleanTempAsync([Assert.Single(preview.Files).Id]);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.BytesFreed);
    }

    [Fact]
    public async Task Directory_junction_is_not_traversed()
    {
        using var fixture = new OptimizationFixture();
        var outside = Create(fixture.Root, "outside/kept.tmp", "keep", 48);
        var link = Path.Combine(fixture.Temp, "linked-directory");
        CleanupTestLinks.CreateJunction(link, Path.GetDirectoryName(outside)!);
        try
        {
            Assert.Empty((await fixture.Service().PreviewTempAsync()).Files);
            Assert.True(File.Exists(outside));
        }
        finally { CleanupTestLinks.RemoveJunction(link); }
    }

    [Fact]
    public async Task Ancestor_replaced_with_junction_after_preview_cannot_delete_its_target()
    {
        using var fixture = new OptimizationFixture();
        var initial = Create(fixture.Temp, "nested/old.tmp", "same", 48);
        var outside = Create(fixture.Root, "outside/old.tmp", "same", 48);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        File.Delete(initial);
        Directory.Delete(Path.GetDirectoryName(initial)!);
        var link = Path.Combine(fixture.Temp, "nested");
        CleanupTestLinks.CreateJunction(link, Path.GetDirectoryName(outside)!);
        try
        {
            var result = await service.CleanTempAsync([Assert.Single(preview.Files).Id]);
            Assert.Equal(0, result.Removed);
            Assert.Equal(1, result.Skipped);
            Assert.True(File.Exists(outside));
        }
        finally { CleanupTestLinks.RemoveJunction(link); }
    }

    [Fact]
    public async Task A_new_preview_invalidates_ids_from_the_previous_preview()
    {
        using var fixture = new OptimizationFixture();
        var path = Create(fixture.Temp, "old.tmp", "old", 48);
        var service = fixture.Service();
        var first = await service.PreviewTempAsync();
        await service.PreviewTempAsync();
        var result = await service.CleanTempAsync([Assert.Single(first.Files).Id]);
        Assert.Equal(0, result.Removed);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Hardlink_to_file_outside_temp_is_skipped_and_cannot_claim_freed_bytes()
    {
        using var fixture = new OptimizationFixture();
        var outside = Create(fixture.Root, "outside.txt", "keep", 48);
        var link = Path.Combine(fixture.Temp, "hardlink.tmp");
        CleanupTestLinks.CreateHardLink(link, outside);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        var result = await service.CleanTempAsync([Assert.Single(preview.Files).Id]);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.BytesFreed);
        Assert.True(File.Exists(link));
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task Preview_bounds_file_count_and_reports_truncation()
    {
        using var fixture = new OptimizationFixture();
        for (var i = 0; i < 4; i++) Create(fixture.Temp, $"{i}.tmp", "old", 48);
        var preview = await fixture.Service(maximumFiles: 2).PreviewTempAsync();
        Assert.Equal(2, preview.Files.Count);
        Assert.True(preview.Truncated);
    }

    [Fact]
    public async Task Precancelled_cleanup_keeps_all_files()
    {
        using var fixture = new OptimizationFixture();
        var path = Create(fixture.Temp, "old.tmp", "old", 48);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CleanTempAsync([Assert.Single(preview.Files).Id], source.Token));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Cancellation_after_a_deletion_preserves_actual_partial_counts()
    {
        using var fixture = new OptimizationFixture();
        Create(fixture.Temp, "a.tmp", "first", 48);
        Create(fixture.Temp, "b.tmp", "second", 48);
        Create(fixture.Temp, "c.tmp", "third", 48);
        using var source = new CancellationTokenSource();
        var cleaning = false;
        var checks = 0;
        var service = new NativeOptimizationService(fixture.Platform, fixture.Journal, fixture.Temp, () =>
        {
            if (cleaning && ++checks == 2) source.Cancel();
            return DateTimeOffset.UtcNow;
        });
        var preview = await service.PreviewTempAsync();
        cleaning = true;
        var result = await service.CleanTempAsync(preview.Files.Select(x => x.Id).ToArray(), source.Token);
        Assert.Equal(1, result.Removed);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(2, Directory.EnumerateFiles(fixture.Temp).Count());
        Assert.Contains("cancel", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.BytesFreed);
        Assert.InRange(result.RecoverableBytes, 5, 6);
    }

    [Fact]
    public async Task Retired_file_survives_new_service_and_restores_exact_content_and_timestamps()
    {
        using var fixture = new OptimizationFixture();
        var path = Create(fixture.Temp, "nested/old.tmp", "recover exactly", 48);
        var written = File.GetLastWriteTimeUtc(path);
        var created = File.GetCreationTimeUtc(path);
        var service = fixture.Service();
        var preview = await service.PreviewTempAsync();
        await service.CleanTempAsync([Assert.Single(preview.Files).Id]);
        Assert.False(File.Exists(path));
        Assert.Empty((await service.PreviewTempAsync()).Files);
        var restarted = fixture.Service();
        var item = Assert.Single(await restarted.ListRecoveryAsync());
        Assert.Equal(Path.Combine("nested", "old.tmp"), item.RelativePath);
        Assert.Equal(15, item.Bytes);
        var restored = await restarted.RestoreRecoveryAsync([item.Id]);
        Assert.Equal(1, restored.Removed);
        Assert.Equal(0, restored.BytesFreed);
        Assert.Equal("recover exactly", File.ReadAllText(path));
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        Assert.Equal(created, File.GetCreationTimeUtc(path));
        Assert.Empty(await restarted.ListRecoveryAsync());
    }

    [Fact]
    public async Task Restore_preserves_conflicting_original_and_recovery_remains_available()
    {
        using var fixture = new OptimizationFixture();
        var path = Create(fixture.Temp, "conflict.tmp", "old", 48);
        var service = fixture.Service();
        await service.CleanTempAsync([Assert.Single((await service.PreviewTempAsync()).Files).Id]);
        var item = Assert.Single(await service.ListRecoveryAsync());
        File.WriteAllText(path, "new original");
        var result = await service.RestoreRecoveryAsync([item.Id]);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal("new original", File.ReadAllText(path));
        Assert.Single(await service.ListRecoveryAsync());
    }

    [Fact]
    public async Task Purge_requires_reviewed_ids_and_reports_bytes_only_after_deletion()
    {
        using var fixture = new OptimizationFixture();
        Create(fixture.Temp, "old.tmp", "old", 48);
        var service = fixture.Service();
        await service.CleanTempAsync([Assert.Single((await service.PreviewTempAsync()).Files).Id]);
        var metadata = Assert.Single(Directory.GetFiles(Path.Combine(fixture.Temp, ".silen-recovery"), "*.json"));
        var id = Path.GetFileNameWithoutExtension(metadata);
        Assert.Equal(0, (await service.PurgeRecoveryAsync([id])).Removed);
        Assert.Single(await service.ListRecoveryAsync());
        var purged = await service.PurgeRecoveryAsync([id, id]);
        Assert.Equal(1, purged.Removed);
        Assert.Equal(3, purged.BytesFreed);
        Assert.Empty(await service.ListRecoveryAsync());
        Assert.Equal(0, (await service.PurgeRecoveryAsync([id])).Removed);
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("traversal")]
    [InlineData("absolute")]
    public async Task Corrupt_or_outside_destination_metadata_cannot_authorize_restoration_or_purge(string kind)
    {
        using var fixture = new OptimizationFixture();
        var outside = Create(fixture.Root, "outside.txt", "preserve", 48);
        Create(fixture.Temp, "old.tmp", "old", 48);
        var service = fixture.Service();
        await service.CleanTempAsync([Assert.Single((await service.PreviewTempAsync()).Files).Id]);
        var item = Assert.Single(await service.ListRecoveryAsync());
        var metadata = Path.Combine(fixture.Temp, ".silen-recovery", item.Id + ".json");
        if (kind == "corrupt") File.WriteAllText(metadata, "{broken");
        else
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(metadata))!;
            json["RelativePath"] = kind == "traversal" ? "../outside.txt" : outside;
            File.WriteAllText(metadata, json.ToJsonString());
        }
        Assert.Equal(0, (await service.RestoreRecoveryAsync([item.Id])).Removed);
        Assert.Equal(0, (await service.PurgeRecoveryAsync([item.Id])).Removed);
        Assert.Empty(await service.ListRecoveryAsync());
        Assert.Equal("preserve", File.ReadAllText(outside));
        Assert.True(File.Exists(Path.Combine(fixture.Temp, ".silen-recovery", item.Id + ".data")));
    }

    [Fact]
    public async Task Recovery_data_replaced_with_hardlink_is_not_restored_or_purged()
    {
        using var fixture = new OptimizationFixture();
        var outside = Create(fixture.Root, "outside.txt", "preserve", 48);
        Create(fixture.Temp, "old.tmp", "old", 48);
        var service = fixture.Service();
        await service.CleanTempAsync([Assert.Single((await service.PreviewTempAsync()).Files).Id]);
        var item = Assert.Single(await service.ListRecoveryAsync());
        var data = Path.Combine(fixture.Temp, ".silen-recovery", item.Id + ".data");
        File.Delete(data);
        CleanupTestLinks.CreateHardLink(data, outside);
        Assert.Equal(0, (await service.PurgeRecoveryAsync([item.Id])).Removed);
        Assert.Equal(0, (await service.RestoreRecoveryAsync([item.Id])).Removed);
        Assert.Equal("preserve", File.ReadAllText(outside));
    }

    [Fact]
    public async Task Restore_cannot_follow_replaced_parent_junction()
    {
        using var fixture = new OptimizationFixture();
        Create(fixture.Temp, "nested/old.tmp", "old", 48);
        var service = fixture.Service();
        await service.CleanTempAsync([Assert.Single((await service.PreviewTempAsync()).Files).Id]);
        var item = Assert.Single(await service.ListRecoveryAsync());
        var nested = Path.Combine(fixture.Temp, "nested");
        var outside = Path.Combine(fixture.Root, "outside");
        Directory.CreateDirectory(outside);
        Directory.Delete(nested);
        CleanupTestLinks.CreateJunction(nested, outside);
        try
        {
            Assert.Equal(0, (await service.RestoreRecoveryAsync([item.Id])).Removed);
            Assert.False(File.Exists(Path.Combine(outside, "old.tmp")));
            Assert.Single(await service.ListRecoveryAsync());
        }
        finally { CleanupTestLinks.RemoveJunction(nested); }
    }

    [Fact]
    public async Task Precancelled_recovery_operation_retains_all_data()
    {
        using var fixture = new OptimizationFixture();
        Create(fixture.Temp, "old.tmp", "old", 48);
        var service = fixture.Service();
        await service.CleanTempAsync([Assert.Single((await service.PreviewTempAsync()).Files).Id]);
        var item = Assert.Single(await service.ListRecoveryAsync());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PurgeRecoveryAsync([item.Id], cancellation.Token));
        Assert.Single(await service.ListRecoveryAsync());
    }

    [Fact]
    public async Task Cancellation_during_purge_keeps_remaining_files_and_counts_completed_work()
    {
        using var fixture = new OptimizationFixture();
        Create(fixture.Temp, "a.tmp", "first", 48);
        Create(fixture.Temp, "b.tmp", "second", 48);
        using var cancellation = new CancellationTokenSource();
        var purging = false;
        var checks = 0;
        var cleanup = new Silen.Windows.NativeTemporaryCleanup(fixture.Temp, () =>
        {
            if (purging && ++checks == 2) cancellation.Cancel();
            return DateTimeOffset.UtcNow;
        }, 5000);
        var preview = await cleanup.PreviewAsync(default);
        await cleanup.CleanAsync(preview.Files.Select(x => x.Id).ToArray(), default);
        var recovery = await cleanup.ListRecoveryAsync(default);
        Assert.Equal(2, recovery.Count);
        purging = true;
        var result = await cleanup.PurgeRecoveryAsync(recovery.Select(x => x.Id).ToArray(), cancellation.Token);
        Assert.Equal(1, result.Removed);
        Assert.Equal(1, result.Skipped);
        Assert.InRange(result.BytesFreed, 5, 6);
        Assert.Contains("cancel", result.Message, StringComparison.OrdinalIgnoreCase);
        purging = false;
        Assert.Single(await cleanup.ListRecoveryAsync(default));
    }

    [Fact]
    public async Task Failed_quarantine_rename_keeps_original_and_removes_only_its_new_manifest()
    {
        using var fixture = new OptimizationFixture();
        var original = Create(fixture.Temp, "old.tmp", "keep original", 48);
        var service = fixture.Service();
        var item = Assert.Single((await service.PreviewTempAsync()).Files);
        var recovery = Path.Combine(fixture.Temp, ".silen-recovery");
        Directory.CreateDirectory(recovery);
        var collision = Path.Combine(recovery, item.Id + ".data");
        File.WriteAllText(collision, "keep existing recovery");
        var result = await service.CleanTempAsync([item.Id]);
        Assert.Equal(0, result.Removed);
        Assert.Equal("keep original", File.ReadAllText(original));
        Assert.Equal("keep existing recovery", File.ReadAllText(collision));
        Assert.False(File.Exists(Path.Combine(recovery, item.Id + ".json")));
    }

    private static string Create(string root, string relative, string text, int ageHours)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-ageHours));
        return path;
    }
}

internal static class CleanupTestLinks
{
    internal static void CreateJunction(string link, string target)
    {

        Directory.CreateDirectory(link);
        var substitute = Encoding.Unicode.GetBytes(@"\??\" + Path.GetFullPath(target));
        var print = Encoding.Unicode.GetBytes(Path.GetFullPath(target));
        var buffer = new byte[16 + substitute.Length + 2 + print.Length + 2];
        BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0);
        BitConverter.GetBytes(checked((ushort)(buffer.Length - 8))).CopyTo(buffer, 4);
        BitConverter.GetBytes(checked((ushort)substitute.Length)).CopyTo(buffer, 10);
        BitConverter.GetBytes(checked((ushort)(substitute.Length + 2))).CopyTo(buffer, 12);
        BitConverter.GetBytes(checked((ushort)print.Length)).CopyTo(buffer, 14);
        substitute.CopyTo(buffer, 16);
        print.CopyTo(buffer, 16 + substitute.Length + 2);
        using var handle = CreateFile(link, 0x40000000, 0, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid || !DeviceIoControl(handle, 0x000900A4, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static void CreateHardLink(string link, string target)
    {
        if (!CreateHardLinkNative(link, target, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static void RemoveJunction(string link)
    {
        using (var handle = CreateFile(link, 0x40000000, 0, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero))
        {
            var buffer = new byte[8];
            BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0);
            if (handle.IsInvalid || !DeviceIoControl(handle, 0x000900AC, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        Directory.Delete(link);
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint sharing, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint control, byte[] input, int inputSize, IntPtr output, int outputSize, out int bytesReturned, IntPtr overlapped);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkNative(string link, string existing, IntPtr security);
}
