using Silen.Core;

namespace Silen.Windows;

internal static class AppUpdatePaths
{
    internal static string CacheRoot => Path.Combine(AppDataPaths.Root, "Updates");
    internal static string Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || path.IndexOf(':', 2) >= 0)
            throw new InvalidDataException("La ruta de actualización no es local y absoluta.");
        var full = Path.GetFullPath(path);
        if (Path.GetPathRoot(full)!.Equals(full.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La raíz de una unidad no es una instalación válida.");
        for (var current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("La actualización no admite enlaces ni redirecciones de archivos.");
        return full;
    }
    internal static string Within(string root, string path)
    {
        root = Validate(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Validate(path);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("La ruta queda fuera de la carpeta de actualización.");
        return full;
    }
    internal static string Archive(string cacheRoot, AppRelease release) => Path.Combine(cacheRoot, release.Sha256.ToUpperInvariant() + ".zip");
}
