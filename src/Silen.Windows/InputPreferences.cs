using System.Text.Json;

namespace Silen.Windows;

public sealed record FilterKeysPreference(uint Flags, uint Wait, uint Delay, uint Repeat, uint Bounce)
{
    public FilterKeysPreference Gaming() => this with { Flags = (Flags | 1u) & ~(4u | 64u), Wait = 0, Delay = 130, Repeat = 10, Bounce = 0 };
}
public sealed record MouseMotionPreference(int Threshold1, int Threshold2, int Acceleration);

internal static class InputPreferences
{
    internal static string Encode<T>(T value) => JsonSerializer.Serialize(value);
    internal static FilterKeysPreference Filter(string text)
    {
        var value = JsonSerializer.Deserialize<FilterKeysPreference>(text) ?? throw new InvalidDataException("Respaldo de teclado incompleto.");
        if (value.Flags > 127 || value.Wait > 20000 || value.Delay > 20000 || value.Repeat > 20000 || value.Bounce > 20000)
            throw new InvalidDataException("El respaldo de teclado contiene valores fuera de rango.");
        return value;
    }
    internal static MouseMotionPreference Mouse(string text)
    {
        var value = JsonSerializer.Deserialize<MouseMotionPreference>(text) ?? throw new InvalidDataException("Respaldo de ratón incompleto.");
        if (value.Threshold1 < 0 || value.Threshold2 < 0 || value.Threshold1 > 32767 || value.Threshold2 > 32767 || value.Acceleration is < 0 or > 2)
            throw new InvalidDataException("El respaldo de ratón contiene valores fuera de rango.");
        return value;
    }
    internal static bool Allows(string tool, OptimizationValueChange value)
    {
        if (value.Key != "" || value.Name != "" || !value.Previous.Exists || !value.Intended.Exists ||
            value.Previous.Kind != Microsoft.Win32.RegistryValueKind.String || value.Intended.Kind != Microsoft.Win32.RegistryValueKind.String) return false;
        try
        {
            return tool switch
            {
                "input-filterkeys" when value.Kind == "FilterKeys" => Filter(value.Intended.Text!) == Filter(value.Previous.Text!).Gaming(),
                "input-mouse-linear" when value.Kind == "MouseMotion" => Mouse(value.Intended.Text!) == new MouseMotionPreference(0, 0, 0) && Mouse(value.Previous.Text!) is not null,
                _ => false
            };
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidDataException) { return false; }
    }
}
