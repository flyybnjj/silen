using System.Security.Cryptography;

namespace Silen.Gaming;

public static class BundledPresentMon
{
    private const string ExpectedSha256 = "B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF";

    public static string Resolve(string? baseDirectory = null)
    {
        var executable = Path.GetFullPath(Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "tools", "PresentMon", "PresentMon.exe"));
        string actualSha256;
        try
        {
            using var file = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read);
            actualSha256 = Convert.ToHexString(SHA256.HashData(file));
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidOperationException("Falta el colector gaming incluido. Vuelve a extraer el paquete completo de Silen para recuperarlo.", error);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("No se pudo comprobar el colector gaming incluido. Comprueba el acceso a la carpeta de Silen y vuelve a intentarlo.", error);
        }
        if (!string.Equals(actualSha256, ExpectedSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("El colector gaming incluido está dañado o fue modificado. Vuelve a extraer el paquete oficial completo de Silen.");
        return executable;
    }
}
