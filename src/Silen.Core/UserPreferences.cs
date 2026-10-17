using System.Text.Json;

namespace Silen.Core;

public sealed record UserPreferences(bool ExpertMode = false, string Theme = "Oscuro");
public interface IUserPreferencesStore
{
    Task<UserPreferences> ReadAsync(CancellationToken token = default);
    Task WriteAsync(UserPreferences value, CancellationToken token = default);
}
public sealed class MemoryUserPreferencesStore : IUserPreferencesStore
{
    private UserPreferences current = new();
    public Task<UserPreferences> ReadAsync(CancellationToken token = default) => Task.FromResult(current);
    public Task WriteAsync(UserPreferences value, CancellationToken token = default) { token.ThrowIfCancellationRequested(); current = value; return Task.CompletedTask; }
}
public sealed class UserPreferencesStore : IUserPreferencesStore
{
    private readonly string path = Path.Combine(AppDataPaths.Root, "user-preferences.json");
    public async Task<UserPreferences> ReadAsync(CancellationToken token = default)
    {
        if (!File.Exists(path)) return new();
        Check(); if (new FileInfo(path).Length > 4096) throw new InvalidDataException("Preferencias demasiado grandes.");
        return JsonSerializer.Deserialize<UserPreferences>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false)) ?? new();
    }
    public async Task WriteAsync(UserPreferences value, CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); Check();
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value), token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private void Check() { if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("El archivo de preferencias es una redirección."); }
}
