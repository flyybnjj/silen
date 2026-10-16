using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Management;

namespace Silen.Windows;



internal static class FortniteSafeFiles
{
    internal const int MaximumSettingsBytes = 512 * 1024;

    internal static string LocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.Length > 1024 ||
            path.StartsWith(@"\\", StringComparison.Ordinal) || path.Length < 3 || path[1] != ':' ||
            path[2] != Path.DirectorySeparatorChar || !char.IsAsciiLetter(path[0]) || path[2..].Contains(':'))
            throw new IOException("La ruta debe pertenecer a una carpeta local del equipo.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    internal static DirectoryLease LockDirectory(string directory, bool create = false)
    {
        var path = LocalPath(directory);
        var chain = new Stack<string>();
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current)) chain.Push(current);
        var handles = new List<SafeFileHandle>();
        try
        {
            while (chain.TryPop(out var current))
            {
                if (!Directory.Exists(current))
                {
                    if (!create) throw new DirectoryNotFoundException("La carpeta ya no está disponible.");
                    Directory.CreateDirectory(current);
                }
                var handle = CreateFile(current, 0, 1 | 2, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
                if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
                handles.Add(handle);
                if (!GetFileInformationByHandle(handle, out var information)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if ((information.Attributes & (uint)FileAttributes.ReparsePoint) != 0 ||
                    (information.Attributes & (uint)FileAttributes.Directory) == 0)
                    throw new IOException("La carpeta contiene un enlace o una redirección.");
            }
            return new DirectoryLease(handles, ResolvePath(handles[^1]));
        }
        catch { foreach (var handle in handles) handle.Dispose(); throw; }
    }

    internal static LockedFile Open(string path, bool requireWritable, bool exclusive = true, int maximumBytes = MaximumSettingsBytes)
    {
        var full = LocalPath(path);
        var directories = LockDirectory(Path.GetDirectoryName(full)!);
        DirectoryLease? packageDirectories = null;
        FileStream? stream = null;
        try
        {
            stream = new FileStream(full, FileMode.Open, requireWritable ? FileAccess.ReadWrite : FileAccess.Read,
                exclusive ? FileShare.None : FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
            if (!GetFileInformationByHandle(stream.SafeFileHandle, out var information)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if ((information.Attributes & (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 ||
                information.NumberOfLinks != 1 ||
                requireWritable && (information.Attributes & (uint)FileAttributes.ReadOnly) != 0)
                throw new IOException("El archivo es de solo lectura, está enlazado o no es un archivo normal.");
            if (stream.Length > maximumBytes) throw new IOException("El archivo supera el tamaño permitido.");
            var finalPath = ResolvePath(stream.SafeFileHandle);
            var expectedPath = Path.Combine(directories.ResolvedPath, Path.GetFileName(full));
            if (!expectedPath.Equals(finalPath, StringComparison.OrdinalIgnoreCase))
            {
                if (!IsPackageAlias(full, finalPath, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CurrentPackageFamily()))
                    throw new IOException("El destino del archivo cambió.");
                packageDirectories = LockDirectory(Path.GetDirectoryName(finalPath)!);
                if (!Path.Combine(packageDirectories.ResolvedPath, Path.GetFileName(full)).Equals(finalPath, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("El destino del archivo cambió.");
            }
            return new LockedFile(full, stream, directories, information, packageDirectories);
        }
        catch { stream?.Dispose(); packageDirectories?.Dispose(); directories.Dispose(); throw; }
    }



    internal static bool IsPackageAlias(string logical, string actual, string localData, string? family)
    {
        if (string.IsNullOrWhiteSpace(family) || family.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-'))) return false;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(localData));
        if (!logical.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        var expected = Path.Combine(root, "Packages", family, "LocalCache", "Local", logical[(root.Length + 1)..]);
        return expected.Equals(actual, StringComparison.OrdinalIgnoreCase);
    }

    private static string? CurrentPackageFamily()
    {
        uint length = 0;
        if (GetCurrentPackageFamilyName(ref length, null) == 122 && length is > 0 and <= 1024)
        {
            var name = new StringBuilder((int)length);
            if (GetCurrentPackageFamilyName(ref length, name) == 0) return name.ToString();
        }


        try
        {
            using var query = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId FROM Win32_Process");
            using var results = query.Get();
            var parents = new Dictionary<uint, uint>();
            foreach (ManagementObject item in results)
                using (item) parents[Convert.ToUInt32(item["ProcessId"])] = Convert.ToUInt32(item["ParentProcessId"]);
            var current = (uint)Environment.ProcessId;
            var seen = new HashSet<uint> { current };
            for (var depth = 0; depth < 8 && parents.TryGetValue(current, out var parent) && parent != 0 && seen.Add(parent); depth++)
            {
                current = parent;
                using var handle = OpenProcess(0x1000, false, current);
                if (handle.IsInvalid) continue;
                length = 0;
                if (GetPackageFamilyName(handle, ref length, null) != 122 || length is 0 or > 1024) continue;
                var name = new StringBuilder((int)length);
                if (GetPackageFamilyName(handle, ref length, name) == 0) return name.ToString();
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or Win32Exception) { }
        return null;
    }

    internal static void WriteDurableNew(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    internal static void WriteLocked(LockedFile existing, byte[] bytes) => existing.WriteBytes(bytes);

    private static string ResolvePath(SafeFileHandle handle)
    {
        var resolved = new StringBuilder(32768);
        var count = GetFinalPathNameByHandle(handle, resolved, (uint)resolved.Capacity, 0);
        if (count is 0 or >= 32768) throw new IOException("No se pudo validar el archivo.");
        var path = resolved.ToString();
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path[4..];
        return Path.TrimEndingDirectorySeparator(path);
    }

    internal sealed class DirectoryLease(List<SafeFileHandle> handles, string resolvedPath) : IDisposable
    {
        internal string ResolvedPath { get; } = resolvedPath;
        public void Dispose() { foreach (var handle in handles) handle.Dispose(); }
    }

    internal sealed class LockedFile(string path, FileStream stream, DirectoryLease directories, FileInformation information, DirectoryLease? packageDirectories = null) : IDisposable
    {
        internal string Path { get; } = path;
        internal long Length => stream.Length;
        internal long LastWriteTicks => ((long)information.WriteTime.dwHighDateTime << 32) | (uint)information.WriteTime.dwLowDateTime;
        internal string Identity => $"{information.VolumeSerial:X8}{information.FileIndexHigh:X8}{information.FileIndexLow:X8}";
        internal bool IsReadOnly => (information.Attributes & (uint)FileAttributes.ReadOnly) != 0;
        internal byte[] ReadBytes()
        {
            stream.Position = 0;
            var bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            return bytes;
        }
        internal void WriteBytes(byte[] bytes)
        {
            if (bytes.Length > MaximumSettingsBytes) throw new IOException("La configuración supera el tamaño permitido.");
            stream.Position = 0;
            stream.Write(bytes);
            stream.SetLength(bytes.Length);
            stream.Flush(flushToDisk: true);
            if (!ReadBytes().AsSpan().SequenceEqual(bytes)) throw new IOException("Windows no confirmó los bytes escritos.");
        }
        public void Dispose() { stream.Dispose(); packageDirectories?.Dispose(); directories.Dispose(); }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FileInformation
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

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint sharing, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref uint length, StringBuilder? name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFamilyName(SafeFileHandle process, ref uint length, StringBuilder? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint id);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);
}
