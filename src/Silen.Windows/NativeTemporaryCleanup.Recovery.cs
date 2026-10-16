using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Silen.Core;

namespace Silen.Windows;

internal sealed partial class NativeTemporaryCleanup
{
    private const string RecoveryFolder = ".silen-recovery";
    private string RecoveryRoot => Path.Combine(root, RecoveryFolder);
    private sealed record RecoveryRecord(string Id, string Batch, string RelativePath, long Bytes, long WriteTicks,
        long CreationTicks, uint Volume, uint IndexHigh, uint IndexLow, uint Attributes, DateTimeOffset CapturedAt);
    private readonly Dictionary<string, RecoveryRecord> reviewedRecovery = new(StringComparer.Ordinal);

    private bool Quarantine(SafeFileHandle source, FileInformation identity, PreviewFile file, string id, string batch, DateTimeOffset captured)
    {
        Directory.CreateDirectory(RecoveryRoot);
        using var directories = LockDirectories(RecoveryRoot);
        var record = new RecoveryRecord(id, batch, Path.GetRelativePath(root, file.Path), file.Bytes, file.WriteTime.Ticks,
            file.CreationTime.Ticks, identity.VolumeSerial, identity.FileIndexHigh, identity.FileIndexLow, identity.Attributes, captured);
        var metadata = Path.Combine(RecoveryRoot, id + ".json");
        using var metadataHandle = CreateFile(metadata, 0xC0010000, 1, IntPtr.Zero, 1, 0x80000000 | 0x00200000, IntPtr.Zero);
        if (!ValidateHandle(metadataHandle, metadata, out _)) return false;
        using var journal = new FileStream(metadataHandle, FileAccess.ReadWrite, 4096, false);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record);
        journal.Write(bytes);
        journal.Flush(true); // La información necesaria para recuperar existe antes del movimiento atómico.
        if (Rename(source, directories.Last, id + ".data")) return true;
        DeleteByHandle(journal.SafeFileHandle);
        return false;
    }

    internal Task<IReadOnlyList<CleanupRecoveryItem>> ListRecoveryAsync(CancellationToken token) => Task.Run(async () =>
    {
        await gate.WaitAsync(token);
        try
        {
            reviewedRecovery.Clear();
            var rows = new List<CleanupRecoveryItem>();
            if (!Directory.Exists(RecoveryRoot)) return (IReadOnlyList<CleanupRecoveryItem>)rows;
            try
            {
                using var directories = LockDirectories(RecoveryRoot);
                foreach (var path in Directory.EnumerateFiles(RecoveryRoot, "*.json").Take(5000))
                {
                    token.ThrowIfCancellationRequested();
                    var id = Path.GetFileNameWithoutExtension(path);
                    try
                    {
                        var record = ReadRecovery(id);
                        if (record is null) continue;
                        using var data = OpenRecovery(record, false);
                        if (data is null) continue;
                        reviewedRecovery.Add(id, record);
                        rows.Add(new(id, record.RelativePath, record.Bytes, record.CapturedAt));
                    }
                    catch (Exception ex) when (IsRecoveryFailure(ex)) { }
                }
            }
            catch (Exception ex) when (IsRecoveryFailure(ex)) { }
            return rows;
        }
        finally { gate.Release(); }
    }, token);

    internal Task<TemporaryCleanupResult> RestoreRecoveryAsync(IReadOnlyCollection<string> ids, CancellationToken token) => ProcessRecoveryAsync(ids, false, token);
    internal Task<TemporaryCleanupResult> PurgeRecoveryAsync(IReadOnlyCollection<string> ids, CancellationToken token) => ProcessRecoveryAsync(ids, true, token);

    private Task<TemporaryCleanupResult> ProcessRecoveryAsync(IReadOnlyCollection<string> ids, bool purge, CancellationToken token) => Task.Run(async () =>
    {
        ArgumentNullException.ThrowIfNull(ids);
        await gate.WaitAsync(token);
        try
        {
            var selected = ids.Distinct(StringComparer.Ordinal).ToArray();
            var completed = 0;
            var skipped = 0;
            long freed = 0;
            for (var index = 0; index < selected.Length; index++)
            {
                if (token.IsCancellationRequested)
                    return new TemporaryCleanupResult(completed, skipped + selected.Length - index, freed,
                        "Operación cancelada. Se conservaron los archivos de recuperación pendientes.");
                var id = selected[index];
                if (id is null || !reviewedRecovery.TryGetValue(id, out var reviewed)) { skipped++; continue; }
                try
                {
                    using var directories = LockDirectories(RecoveryRoot);
                    var record = ReadRecovery(id);
                    if (record is null || record != reviewed) { skipped++; continue; }
                    if (token.IsCancellationRequested)
                        return new TemporaryCleanupResult(completed, skipped + selected.Length - index, freed,
                            "Operación cancelada. Se conservaron los archivos de recuperación pendientes.");
                    using var data = OpenRecovery(record, true);
                    if (data is null) { skipped++; continue; }
                    bool changed;
                    if (purge) changed = DeleteByHandle(data);
                    else
                    {
                        var target = OriginalPath(record.RelativePath);
                        if (target is null) { skipped++; continue; }
                        using var originalDirectories = LockDirectories(Path.GetDirectoryName(target)!);
                        changed = Rename(data, originalDirectories.Last, Path.GetFileName(target));
                    }
                    if (!changed) { skipped++; continue; }
                    completed++;
                    if (purge) freed = checked(freed + record.Bytes);
                    reviewedRecovery.Remove(id);
                    // El metadato residual tras una interrupción no autoriza otra acción: falta el archivo con esa identidad.
                    using var metadata = OpenMetadata(id, true);
                    if (metadata is not null) DeleteByHandle(metadata);
                }
                catch (Exception ex) when (IsRecoveryFailure(ex)) { skipped++; }
            }
            return new TemporaryCleanupResult(completed, skipped, freed, purge
                ? $"{SpanishText.Form(completed, "Se purgó", "Se purgaron")} definitivamente {SpanishText.Count(completed, "archivo", "archivos")} y {SpanishText.Form(skipped, "se omitió", "se omitieron")} {skipped}."
                : $"{SpanishText.Form(completed, "Se restauró", "Se restauraron")} {SpanishText.Count(completed, "archivo", "archivos")} sin sobrescribir destinos y {SpanishText.Form(skipped, "se omitió", "se omitieron")} {skipped}.");
        }
        finally { gate.Release(); }
    }, token);

    private RecoveryRecord? ReadRecovery(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) return null;
        using var handle = OpenMetadata(id, false);
        if (handle is null) return null;
        using var stream = new FileStream(handle, FileAccess.Read);
        if (stream.Length is <= 0 or > 16384) return null;
        var record = JsonSerializer.Deserialize<RecoveryRecord>(stream, new JsonSerializerOptions { MaxDepth = 4 });
        if (record is null || record.Id != id || !Guid.TryParseExact(record.Batch, "N", out _) || record.Bytes < 0 ||
            record.WriteTicks <= 0 || record.CreationTicks <= 0 || record.CapturedAt > clock().AddMinutes(1) ||
            OriginalPath(record.RelativePath) is null) return null;
        return record;
    }

    private string? OriginalPath(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Length > 1024 || Path.IsPathRooted(relative) || relative.Contains(':')) return null;
        var segments = relative.Split(['/', '\\']);
        if (segments.Any(s => string.IsNullOrWhiteSpace(s) || s is "." or ".." || s.EndsWith('.') || s.EndsWith(' ') || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) ||
            segments[0].Equals(RecoveryFolder, StringComparison.OrdinalIgnoreCase)) return null;
        var target = Path.GetFullPath(Path.Combine(root, relative));
        return WithinRoot(target) ? target : null;
    }

    private SafeFileHandle? OpenMetadata(string id, bool delete)
    {
        if (!Guid.TryParseExact(id, "N", out _)) return null;
        var path = Path.Combine(RecoveryRoot, id + ".json");
        var handle = CreateFile(path, 0x80000000 | (delete ? 0x10000u : 0), 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (!ValidateHandle(handle, path, out _)) { handle.Dispose(); return null; }
        return handle;
    }

    private SafeFileHandle? OpenRecovery(RecoveryRecord record, bool delete)
    {
        var path = Path.Combine(RecoveryRoot, record.Id + ".data");
        var handle = CreateFile(path, 0x80000000 | (delete ? 0x10000u : 0), 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (!ValidateHandle(handle, path, out var identity) || identity.VolumeSerial != record.Volume ||
            identity.FileIndexHigh != record.IndexHigh || identity.FileIndexLow != record.IndexLow ||
            (((long)identity.SizeHigh << 32) | identity.SizeLow) != record.Bytes ||
            FromFileTime(identity.WriteTime).Ticks != record.WriteTicks || FromFileTime(identity.CreationTime).Ticks != record.CreationTicks ||
            identity.Attributes != record.Attributes)
        { handle.Dispose(); return null; }
        return handle;
    }

    private bool ValidateHandle(SafeFileHandle handle, string path, out FileInformation information)
    {
        information = default;
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out information) || information.NumberOfLinks != 1 ||
            (information.Attributes & (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) return false;
        return FinalPath(handle)?.Equals(path, StringComparison.OrdinalIgnoreCase) == true && SafeAncestors(path, true);
    }

    private static string? FinalPath(SafeFileHandle handle)
    {
        var buffer = new StringBuilder(32768);
        var count = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (count is 0 or >= 32768) return null;
        var path = buffer.ToString();
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + path[8..];
        return path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ? path[4..] : path;
    }

    private DirectoryLocks LockDirectories(string path)
    {
        if (!SafeAncestors(path, false)) throw new IOException("La ruta de recuperación contiene enlaces o no está disponible.");
        var chain = new Stack<string>();
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current)) chain.Push(current);
        var locks = new DirectoryLocks();
        try
        {
            while (chain.TryPop(out var directory))
            {
                var handle = CreateFile(directory, 0x80 | (chain.Count == 0 ? 0x2u : 0), 3, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
                locks.Handles.Add(handle);
                if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info) ||
                    (info.Attributes & (uint)FileAttributes.ReparsePoint) != 0 || !directory.Equals(FinalPath(handle), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("No se pudo bloquear una carpeta segura para la recuperación.");
            }
            return locks;
        }
        catch { locks.Dispose(); throw; }
    }

    private sealed class DirectoryLocks : IDisposable
    {
        internal List<SafeFileHandle> Handles { get; } = [];
        internal SafeFileHandle Last => Handles[^1];
        public void Dispose() { for (var i = Handles.Count - 1; i >= 0; i--) Handles[i].Dispose(); }
    }

    private static bool DeleteByHandle(SafeFileHandle handle)
    {
        var disposition = new FileDisposition { DeleteFile = true };
        return SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf<FileDisposition>());
    }

    private static bool Rename(SafeFileHandle source, SafeFileHandle destinationDirectory, string leaf)
    {
        var name = Encoding.Unicode.GetBytes(leaf);
        var rootOffset = IntPtr.Size == 8 ? 8 : 4;
        var lengthOffset = rootOffset + IntPtr.Size;
        var nameOffset = lengthOffset + 4;
        var bytes = new byte[nameOffset + name.Length + 2];
        BitConverter.GetBytes(destinationDirectory.DangerousGetHandle().ToInt64()).AsSpan(0, IntPtr.Size).CopyTo(bytes.AsSpan(rootOffset));
        BitConverter.GetBytes(name.Length).CopyTo(bytes, lengthOffset);
        name.CopyTo(bytes, nameOffset);
        // ReplaceIfExists=false: no sobrescribir archivos originales ni otros elementos de recuperación.
        // FILE_RENAME_INFORMATION usa RootDirectory y un nombre simple; no vuelve a resolver una ruta de destino.
        // https://learn.microsoft.com/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information
        return NtSetInformationFile(source, out _, bytes, (uint)bytes.Length, 10) == 0;
    }

    private static bool IsRecoveryFailure(Exception ex) => IsFileFailure(ex) || ex is JsonException or InvalidOperationException or OverflowException;
    [StructLayout(LayoutKind.Sequential)] private struct IoStatusBlock { internal IntPtr Status; internal UIntPtr Information; }
    [DllImport("ntdll.dll")] private static extern int NtSetInformationFile(SafeFileHandle handle, out IoStatusBlock status, byte[] information, uint length, int informationClass);
}
