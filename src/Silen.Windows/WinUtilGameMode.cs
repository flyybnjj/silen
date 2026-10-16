using System.Security.Cryptography;
using System.Text.Json;

namespace Silen.Windows;

internal static class WinUtilGameMode
{
    private const string ExpectedHash = "354CF19DD3FDB0354C8015137CE9A013C04D0EE9D781C4C460351CF82EE53A9B";
    internal static List<(string Name, int Value)> Read()
    {
        using var source = typeof(WinUtilGameMode).Assembly.GetManifestResourceStream("Silen.WinUtil.GameMode.json")
            ?? throw new InvalidDataException("Falta el perfil integrado de WinUtil. Recupera el paquete de Silen.");
        using var bytes = new MemoryStream();
        source.CopyTo(bytes);
        var data = bytes.ToArray();
        if (data.Length > 65536 || Convert.ToHexString(SHA256.HashData(data)) != ExpectedHash)
            throw new InvalidDataException("La selección integrada de WinUtil no coincide con su procedencia fijada.");
        using var document = JsonDocument.Parse(data);
        var rows = document.RootElement.GetProperty("WPFToggleGameMode").GetProperty("registry").EnumerateArray().ToList();
        if (rows.Count != 2) throw new InvalidDataException("El perfil de Modo Juego no es compatible.");
        var result = new List<(string, int)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var name = row.GetProperty("Name").GetString()!;
            if (row.GetProperty("Path").GetString() != @"HKCU:\Software\Microsoft\GameBar" ||
                row.GetProperty("Type").GetString() != "DWord" || row.GetProperty("Value").GetString() != "1" ||
                name is not ("AllowAutoGameMode" or "AutoGameModeEnabled") || !names.Add(name))
                throw new InvalidDataException("El perfil integrado contiene un destino no permitido.");
            result.Add((name, 1));
        }
        return result;
    }
}
