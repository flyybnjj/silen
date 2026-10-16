using System.Text.Json;

namespace Silen.Windows;

internal sealed class FileApplicationTimerStore(string directory) : IApplicationTimerStore
{
    private readonly string root = Path.GetFullPath(directory);
    private string PathFor(string name) => Path.Combine(root, name);
    private void Check()
    {
        Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("La carpeta de perfiles es una redirección; se conservó.");
        foreach (var name in new[] { "profiles.json", "runtime.lock" })
            if (File.Exists(PathFor(name)) && (File.GetAttributes(PathFor(name)) & FileAttributes.ReparsePoint) != 0) throw new IOException("El archivo de perfiles es una redirección; se conservó.");
    }
    public IDisposable AcquireOwner() { Check(); return new FileStream(PathFor("runtime.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
    public async Task<string[]> ReadAsync(CancellationToken token)
    {
        Check(); var path = PathFor("profiles.json"); if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 131072) throw new InvalidDataException("El archivo de perfiles supera su límite.");
        var values = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false)) ?? throw new InvalidDataException("Perfil de temporizador incompleto.");
        if (values.Length > 64 || values.Any(value => !ApplicationGpuPreference.HasPathShape(value))) throw new InvalidDataException("Perfil de temporizador incompatible.");
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public async Task WriteAsync(string[] paths, CancellationToken token)
    {
        Check();
        if (paths.Length > 64 || paths.Any(path => !ApplicationGpuPreference.HasPathShape(path))) throw new InvalidDataException("Perfil de temporizador incompatible.");
        var json = JsonSerializer.Serialize(paths);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 131072) throw new InvalidDataException("Los perfiles superan el tamaño permitido; se conservó el archivo anterior.");
        var temporary = PathFor("." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, json, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); File.Move(temporary, PathFor("profiles.json"), true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
