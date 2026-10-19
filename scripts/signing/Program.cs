using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

try
{
    if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("La firma local requiere Windows y DPAPI.");
    if (args.Length < 2) throw new ArgumentException("Uso: init|sign|verify <raíz-repo> [tag] [zip] [manifiesto] [clave-dpapi]");
    var repository = Path.GetFullPath(args[1]);
    var publicPath = Path.Combine(repository, "src", "Silen.Windows", "UpdateSigningPublicKey.pem");
    var keyPath = args.Length > 5 ? Path.GetFullPath(args[5]) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Silen", "Signing", "release-key.dpapi");
    if (keyPath.StartsWith(repository.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("La clave privada debe quedar fuera del repositorio.");
    if (args[0] == "init")
    {
        if (File.Exists(keyPath) || File.Exists(publicPath) && !string.IsNullOrWhiteSpace(File.ReadAllText(publicPath)))
            throw new InvalidOperationException("Ya existe una clave. No se reemplaza ni se rota automáticamente.");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateBytes = key.ExportPkcs8PrivateKey();
        try
        {
            var encrypted = Protection.Transform(privateBytes, true);
            Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
            using (var file = new FileStream(keyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) file.Write(encrypted);
            File.WriteAllText(publicPath, key.ExportSubjectPublicKeyInfoPem() + Environment.NewLine);
        }
        finally { CryptographicOperations.ZeroMemory(privateBytes); }
        Console.WriteLine("Clave privada creada y protegida con DPAPI para el usuario actual. Clave pública incorporada; recompila antes de distribuir.");
        return 0;
    }
    if (args.Length < 5 || !Regex.IsMatch(args[2], @"\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*)){0,2}\z")) throw new ArgumentException("Indica tag estable, ZIP y manifiesto.");
    var tag = args[2];
    var zip = Path.GetFullPath(args[3]);
    var manifest = Path.GetFullPath(args[4]);
    if (Path.GetFileName(zip) != "Silen-win-x64.zip") throw new InvalidDataException("El ZIP debe llamarse Silen-win-x64.zip.");
    var size = new FileInfo(zip).Length;
    if (size is <= 0 or > 512L * 1024 * 1024) throw new InvalidDataException("Tamaño de ZIP no permitido.");
    string sha256;
    using (var file = File.OpenRead(zip)) sha256 = Convert.ToHexString(SHA256.HashData(file));
    var expectedPayload = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, repository = "flyybnjj/silen", tag, asset = "Silen-win-x64.zip", size, sha256 });
    using var trusted = ECDsa.Create();
    trusted.ImportFromPem(File.ReadAllText(publicPath));
    if (trusted.KeySize != 256) throw new InvalidDataException("La clave pública debe ser ECDSA P-256.");
    if (args[0] == "sign")
    {
        if (new FileInfo(keyPath).Length is <= 0 or > 8192) throw new InvalidDataException("Archivo de clave no válido.");
        var privateBytes = Protection.Transform(File.ReadAllBytes(keyPath), false);
        try
        {
            using var key = ECDsa.Create();
            key.ImportPkcs8PrivateKey(privateBytes, out var consumed);
            if (consumed != privateBytes.Length || !CryptographicOperations.FixedTimeEquals(key.ExportSubjectPublicKeyInfo(), trusted.ExportSubjectPublicKeyInfo()))
                throw new InvalidDataException("La clave de firma no coincide con la clave pública incorporada.");
            var signature = key.SignData(expectedPayload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            File.WriteAllBytes(manifest, JsonSerializer.SerializeToUtf8Bytes(new { payload = Convert.ToBase64String(expectedPayload), signature = Convert.ToBase64String(signature) }));
        }
        finally { CryptographicOperations.ZeroMemory(privateBytes); }
    }
    else if (args[0] != "verify") throw new ArgumentException("Acción desconocida.");
    if (new FileInfo(manifest).Length is <= 0 or > 32768) throw new InvalidDataException("Manifiesto fuera de límites.");
    using var document = JsonDocument.Parse(File.ReadAllBytes(manifest));
    var payload = Convert.FromBase64String(document.RootElement.GetProperty("payload").GetString()!);
    var signed = Convert.FromBase64String(document.RootElement.GetProperty("signature").GetString()!);
    if (!payload.AsSpan().SequenceEqual(expectedPayload) || signed.Length != 64 || !trusted.VerifyData(payload, signed, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
        throw new InvalidDataException("Firma del editor o identidad del ZIP incorrectas.");
    Console.WriteLine("Firma ECDSA P-256/SHA-256 verificada para el ZIP y tag indicados. No es una firma Authenticode.");
    return 0;
}
catch (Exception)
{
    // No imprimir excepciones criptográficas, argumentos ni material de clave.
    Console.Error.WriteLine("No se pudo completar la firma/verificación. Revisa el tag, ZIP, manifiesto y la clave DPAPI del usuario; no se modificó la confianza automáticamente.");
    return 1;
}

internal static class Protection
{
    internal static byte[] Transform(byte[] data, bool protect)
    {
        var input = new Blob { Length = data.Length, Data = Marshal.AllocHGlobal(data.Length) };
        var output = new Blob();
        try
        {
            Marshal.Copy(data, 0, input.Data, data.Length);
            var ok = protect ? CryptProtectData(ref input, "Silen release signing", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok || output.Length is <= 0 or > 8192) throw new CryptographicException();
            var result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            for (var i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero)
            {
                for (var i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0);
                LocalFree(output.Data);
            }
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { internal int Length; internal IntPtr Data; }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
