using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Silen.Windows;

internal static class FortniteGpuPreference
{
    private static readonly Regex Tokens = new(@"(?:^|;)\s*GpuPreference\s*=\s*([^;]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal static bool HasFortnitePathShape(string path) => Path.IsPathFullyQualified(path) &&
        path.EndsWith(Path.Combine("FortniteGame", "Binaries", "Win64", "FortniteClient-Win64-Shipping.exe"), StringComparison.OrdinalIgnoreCase);

    internal static string ValidateExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !HasFortnitePathShape(path)) throw new ArgumentException("Selecciona una instalación verificada de Fortnite.");
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) throw new InvalidOperationException("El ejecutable de Fortnite ya no existe. Vuelve a detectarlo.");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("No se acepta un enlace como ejecutable de Fortnite.");
        for (var parent = Directory.GetParent(path); parent is not null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("La carpeta de Fortnite contiene un enlace. Selecciona su ubicación real.");
        return path;
    }

    internal static bool? IsHighPerformance(RegistryValueState state)
    {
        if (!state.Exists || state.Kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString)) return null;
        var matches = Tokens.Matches(state.Text ?? "");
        return matches.Count == 1 && int.TryParse(matches[0].Groups[1].Value.Trim(), out var preference) && preference is >= 0 and <= 2 ? preference == 2 : null;
    }

    internal static string HighPerformanceText(RegistryValueState state)
    {
        if (!state.Exists || state.Kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString)) return "GpuPreference=2;";
        var text = state.Text ?? "";
        if (text.Length > 65536) throw new InvalidDataException("La preferencia GPU existente es demasiado grande.");
        var matches = Tokens.Matches(text);
        if (matches.Count > 1 || matches.Count == 1 &&
            (!int.TryParse(matches[0].Groups[1].Value.Trim(), out var number) || number is < 0 or > 2))
            throw new InvalidDataException("La preferencia GPU contiene valores incompatibles. No se reemplazó.");
        if (matches.Count == 0) return text + (text.Length > 0 && !text.EndsWith(';') ? ";" : "") + "GpuPreference=2;";
        var value = matches[0].Groups[1];
        return text[..value.Index] + "2" + text[(value.Index + value.Length)..];
    }
}
