using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Silen.Core;

[SupportedOSPlatform("windows")]
public sealed class BloomKeyStore(string? path = null) : IBloomKeyStore
{
    public static BloomKeyStore ForProvider(BloomProvider provider) => provider switch
    {
        BloomProvider.Gemini => new(),
        BloomProvider.OpenAI => new(Path.Combine(Path.GetDirectoryName(DefaultPath)!, "bloom-openai.dat")),
        BloomProvider.Anthropic => new(Path.Combine(Path.GetDirectoryName(DefaultPath)!, "bloom-anthropic.dat")),
        _ => throw new BloomException("Proveedor no admitido.")
    };
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Silen", "bloom.dat");
    private readonly string file = Path.GetFullPath(path ?? DefaultPath);
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Silen.Bloom.ApiKey.v1");
    public string? Read()
    {
        try
        {
            CheckPath();
            if (!File.Exists(file)) return null;
            if (new FileInfo(file).Length is < 1 or > 16384) throw new CryptographicException();
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(file), Entropy, DataProtectionScope.CurrentUser);
            try { var key = Encoding.UTF8.GetString(plain); BloomGeminiClient.ValidateKey(key); return key; }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or BloomException)
        { throw new BloomException("No se pudo leer la clave cifrada de Bloom. Vuelve a guardarla o elimina la configuración local."); }
    }
    public void Save(string key)
    {
        BloomGeminiClient.ValidateKey(key);
        string? temporary = null;
        var plain = Encoding.UTF8.GetBytes(key);
        try
        {
            CheckPath(); Directory.CreateDirectory(Path.GetDirectoryName(file)!); CheckPath();
            var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            temporary = Path.Combine(Path.GetDirectoryName(file)!, ".bloom-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { output.Write(encrypted); output.Flush(true); }
            CheckPath(); File.Move(temporary, file, true);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        { throw new BloomException("No se pudo guardar la clave cifrada de Bloom. Revisa los permisos de tu perfil."); }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            if (temporary is not null) try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    public void Delete()
    {
        try { CheckPath(); File.Delete(file); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new BloomException("No se pudo eliminar la clave local de Bloom."); }
    }
    private void CheckPath()
    {
        if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Destino redirigido.");
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(file)!); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Carpeta redirigida.");
    }
}

