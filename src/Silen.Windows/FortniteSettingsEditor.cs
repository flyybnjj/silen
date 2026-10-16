using System.Text;
using System.Text.RegularExpressions;

namespace Silen.Windows;

internal static partial class FortniteSettingsEditor
{
    private static readonly HashSet<string> QualityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "sg.ViewDistanceQuality", "sg.AntiAliasingQuality", "sg.ShadowQuality", "sg.GlobalIlluminationQuality",
        "sg.ReflectionQuality", "sg.PostProcessQuality", "sg.TextureQuality", "sg.EffectsQuality",
        "sg.FoliageQuality", "sg.ShadingQuality"
    };

    internal static (byte[] Bytes, int Recognized, int Changed) Transform(byte[] original)
    {
        var (encoding, prefix) = DetectEncoding(original);
        var text = encoding.GetString(original, prefix, original.Length - prefix);
        if (text.Contains('\0')) throw new IOException("La configuración no tiene una codificación de texto compatible.");
        var section = "";
        var recognized = 0;
        var changed = 0;
        var edited = Lines().Replace(text, match =>
        {
            var line = match.Value;
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1].Trim();
                return line;
            }
            if (trimmed.StartsWith(';') || trimmed.StartsWith('#')) return line;
            var equals = line.IndexOf('=');
            if (equals <= 0) return line;
            var key = line[..equals].Trim();
            string? target = null;
            if (section.Equals("ScalabilityGroups", StringComparison.OrdinalIgnoreCase) && QualityKeys.Contains(key)) target = "0";
            else if (section.Equals("/Script/FortniteGame.FortGameUserSettings", StringComparison.OrdinalIgnoreCase) ||
                     section.Equals("FortGameUserSettings", StringComparison.OrdinalIgnoreCase))
                target = key.ToLowerInvariant() switch { "busevsync" or "bmotionblur" => "False", "bshowfps" => "True", _ => null };
            if (target is null) return line;
            var end = line.IndexOfAny([';', '#'], equals + 1);
            if (end < 0) end = line.Length;
            var start = equals + 1;
            while (start < end && char.IsWhiteSpace(line[start])) start++;
            while (end > start && char.IsWhiteSpace(line[end - 1])) end--;
            recognized++;
            if (line[start..end].Equals(target, StringComparison.OrdinalIgnoreCase)) return line;
            changed++;
            return line[..start] + target + line[end..];
        });
        var content = encoding.GetBytes(edited);
        return (original[..prefix].Concat(content).ToArray(), recognized, changed);
    }

    private static (Encoding Encoding, int Prefix) DetectEncoding(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe, 0, 0 }) || bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xfe, 0xff }))
            throw new IOException("La configuración UTF-32 no se modifica.");
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe })) return (new UnicodeEncoding(false, false, true), 2);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff })) return (new UnicodeEncoding(true, false, true), 2);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) return (new UTF8Encoding(false, true), 3);
        var utf8 = new UTF8Encoding(false, true);
        try { _ = utf8.GetString(bytes); return (utf8, 0); }
        catch (DecoderFallbackException) { return (Encoding.Latin1, 0); }
    }

    [GeneratedRegex(@"[^\r\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex Lines();
}
