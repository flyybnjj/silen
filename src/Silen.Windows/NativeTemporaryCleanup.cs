using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Silen.Core;

namespace Silen.Windows;

internal sealed partial class NativeTemporaryCleanup
{
    private sealed record PreviewFile(string Path, long Bytes, DateTime WriteTime, DateTime CreationTime, uint Volume, uint IndexHigh, uint IndexLow);
    private readonly string root;
    private readonly Func<DateTimeOffset> clock;
    private readonly int maximumFiles;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Dictionary<string, PreviewFile> preview = new(StringComparer.Ordinal);

    internal NativeTemporaryCleanup(string root, Func<DateTimeOffset> clock, int maximumFiles)
    {
        this.root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        this.clock = clock;
        this.maximumFiles = Math.Clamp(maximumFiles, 1, 5000);
    }
    internal NativeTemporaryCleanup CreateIndependentBatch() => new(root, clock, maximumFiles);

    internal Task<TemporaryCleanupPreview> PreviewAsync(CancellationToken token) => Task.Run(async () =>
    {
        await gate.WaitAsync(token);
        try
        {
            preview.Clear();
            if (!Directory.Exists(root)) return new TemporaryCleanupPreview([], 0, false, "No hay carpeta de temporales del usuario.");
            if (!SafeAncestors(root, includeFile: false)) return new TemporaryCleanupPreview([], 0, false, "La carpeta de temporales contiene un enlace o una redirección. No se recorrió.");
            var next = new Dictionary<string, PreviewFile>(StringComparer.Ordinal);
            var rows = new List<TemporaryFileItem>();
            var pending = new Stack<string>();
            pending.Push(root);
            var inspected = 0;
            var truncated = false;
            long total = 0;
            while (pending.Count > 0 && !truncated)
            {
                token.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                if (!SafeAncestors(directory, includeFile: false)) continue;
                try
                {
                    foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                    {
                        token.ThrowIfCancellationRequested();
                        if (path.Equals(RecoveryRoot, StringComparison.OrdinalIgnoreCase)) continue;
                        if (++inspected > 50000 || rows.Count >= maximumFiles) { truncated = true; break; }
                        try
                        {
                            var attributes = File.GetAttributes(path);
                            if ((attributes & FileAttributes.ReparsePoint) != 0 || !SafeAncestors(path, includeFile: true)) continue;
                            if ((attributes & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                            var file = new FileInfo(path);
                            if (!file.Exists || file.LastWriteTimeUtc > clock().UtcDateTime.AddHours(-24)) continue;
                            var id = Guid.NewGuid().ToString("N");
                            using var snapshot = CreateFile(path, 0x80, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
                            if (snapshot.IsInvalid || !GetFileInformationByHandle(snapshot, out var identity)) continue;
                            next.Add(id, new(Path.GetFullPath(path), file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc, identity.VolumeSerial, identity.FileIndexHigh, identity.FileIndexLow));
                            rows.Add(new(id, Path.GetRelativePath(root, path), file.Length));
                            total = checked(total + file.Length);
                        }
                        catch (Exception ex) when (IsFileFailure(ex)) {  }
                    }
                }
                catch (Exception ex) when (IsFileFailure(ex)) {  }
            }
            preview = next;
            return new TemporaryCleanupPreview(rows, total, truncated, rows.Count == 0
                ? "No se encontraron archivos elegibles de al menos 24 horas."
                : "Temporales del usuario de al menos 24 horas. Los seleccionados se moverán a recuperación; el espacio se libera únicamente al purgarlos después.");
        }
        finally { gate.Release(); }
    }, token);

    internal Task<TemporaryCleanupResult> CleanAsync(IReadOnlyCollection<string> ids, CancellationToken token) => Task.Run(async () =>
    {
        ArgumentNullException.ThrowIfNull(ids);
        token.ThrowIfCancellationRequested();
        await gate.WaitAsync(token);
        try
        {
            var selected = ids.Distinct(StringComparer.Ordinal).ToArray();
            var removed = 0;
            var skipped = 0;
            long bytes = 0;
            var batch = Guid.NewGuid().ToString("N");
            for (var index = 0; index < selected.Length; index++)
            {
                if (token.IsCancellationRequested)
                    return new TemporaryCleanupResult(removed, skipped + selected.Length - index, 0,
                        "Limpieza cancelada. Los archivos ya retirados siguen disponibles en recuperación.", bytes, removed > 0 ? batch : null);
                var id = selected[index];
                if (id is null || !preview.Remove(id, out var file)) { skipped++; continue; }
                var now = clock();
                if (token.IsCancellationRequested)
                    return new TemporaryCleanupResult(removed, skipped + selected.Length - index, 0,
                        "Limpieza cancelada. Los archivos ya retirados siguen disponibles en recuperación.", bytes, removed > 0 ? batch : null);
                if (RetireValidated(file, now, id, batch)) { removed++; bytes = checked(bytes + file.Bytes); }
                else skipped++;
            }
            return new TemporaryCleanupResult(removed, skipped, 0,
                $"{SpanishText.Form(removed, "Se retiró", "Se retiraron")} {SpanishText.Count(removed, "archivo", "archivos")} a recuperación y {SpanishText.Form(skipped, "se omitió", "se omitieron")} {skipped}. No se liberó espacio todavía; puedes restaurar el lote o revisar su purga definitiva.", bytes, removed > 0 ? batch : null);
        }
        finally { gate.Release(); }
    }, token);

    private bool RetireValidated(PreviewFile file, DateTimeOffset now, string id, string batch)
    {
        try
        {
            if (!WithinRoot(file.Path) || !SafeAncestors(file.Path, includeFile: true)) return false;
            using var sourceDirectories = LockDirectories(Path.GetDirectoryName(file.Path)!);
            using var handle = CreateFile(file.Path, 0x00010000 | 0x80000000, 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
            if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info)) return false;
            if ((info.Attributes & (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 || info.NumberOfLinks != 1) return false;
            var length = ((long)info.SizeHigh << 32) | info.SizeLow;
            var write = FromFileTime(info.WriteTime);
            var created = FromFileTime(info.CreationTime);
            if (length != file.Bytes || write != file.WriteTime || created != file.CreationTime || write > now.UtcDateTime.AddHours(-24) ||
                info.VolumeSerial != file.Volume || info.FileIndexHigh != file.IndexHigh || info.FileIndexLow != file.IndexLow) return false;
            var buffer = new StringBuilder(32768);
            var size = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (size is 0 or >= 32768) return false;
            var resolved = buffer.ToString();
            if (resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) resolved = @"\\" + resolved[8..];
            else if (resolved.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)) resolved = resolved[4..];
            if (!resolved.Equals(file.Path, StringComparison.OrdinalIgnoreCase) || !WithinRoot(resolved) || !SafeAncestors(file.Path, includeFile: true)) return false;
            return Quarantine(handle, info, file, id, batch, now);
        }
        catch (Exception ex) when (IsFileFailure(ex)) { return false; }
    }

    private bool WithinRoot(string path) => Path.GetFullPath(path).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private bool SafeAncestors(string path, bool includeFile)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!full.Equals(root, StringComparison.OrdinalIgnoreCase) && !WithinRoot(full)) return false;
            var current = includeFile ? full : Path.GetFullPath(path);
            while (current is not null)
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
                current = Path.GetDirectoryName(current);
            }
            return true;
        }
        catch (Exception ex) when (IsFileFailure(ex)) { return false; }
    }

    private static DateTime FromFileTime(System.Runtime.InteropServices.ComTypes.FILETIME value) =>
        DateTime.FromFileTimeUtc(((long)value.dwHighDateTime << 32) | (uint)value.dwLowDateTime);

    private static bool IsFileFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException
        or Win32Exception or System.Security.SecurityException;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME AccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME WriteTime;
        public uint VolumeSerial;
        public uint SizeHigh;
        public uint SizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
    [StructLayout(LayoutKind.Sequential)] private struct FileDisposition { [MarshalAs(UnmanagedType.U1)] public bool DeleteFile; }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint sharing, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, uint informationClass, ref FileDisposition information, uint size);
}
