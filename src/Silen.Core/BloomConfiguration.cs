using System.Text.Json;

namespace Silen.Core;

public enum BloomProvider { Gemini, OpenAI, Anthropic }
public enum BloomState { Idle, Analyzing, Thinking, Success, Warning, Error }
public sealed record BloomConfiguration(BloomProvider Provider, string Model)
{
    public static BloomConfiguration Default => new(BloomProvider.Gemini, BloomGeminiClient.ModelId);
    public static IReadOnlyList<string> Models(BloomProvider provider) => provider switch
    {
        BloomProvider.Gemini => [BloomGeminiClient.ModelId, "gemini-2.5-flash"],
        BloomProvider.OpenAI => ["gpt-5-mini", "gpt-5.4-mini"],
        BloomProvider.Anthropic => ["claude-haiku-4-5-20251001", "claude-haiku-5-5"],
        _ => []
    };
    public static string DefaultModel(BloomProvider provider) => Models(provider).FirstOrDefault()
        ?? throw new BloomException("Proveedor de Bloom no admitido.");
    public void Validate()
    {
        if (!Models(Provider).Contains(Model, StringComparer.Ordinal)) throw new BloomException("Selecciona un modelo del catálogo del proveedor.");
    }
    public string KeyPage => Provider switch
    {
        BloomProvider.Gemini => "https://aistudio.google.com/app/apikey",
        BloomProvider.OpenAI => "https://platform.openai.com/api-keys",
        BloomProvider.Anthropic => "https://platform.claude.com/settings/keys",
        _ => throw new BloomException("Proveedor no admitido.")
    };
}

public sealed record BloomUsage(long InputTokens = 0, long OutputTokens = 0)
{
    public long TotalTokens => AddCounts(InputTokens, OutputTokens);
    public static BloomUsage operator +(BloomUsage left, BloomUsage right) => new(AddCounts(left.InputTokens, right.InputTokens), AddCounts(left.OutputTokens, right.OutputTokens));
    internal static long AddCounts(long left, long right)
    {
        if (left < 0 || right < 0) throw new BloomException("El proveedor devolvió un consumo de tokens inválido.");
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }
}

public static class BloomLimits
{
    public const int InputCharacters = 4000;
    public const int ContextCharacters = 64000;
    public const int OutputTokens = 2048;
    public const int SessionTokens = 60000;
    public const int ResponseBytes = 2 * 1024 * 1024;
    public static void ValidateContext(IReadOnlyList<JsonElement> contents)
    {
        if (contents.Count > 48 || contents.Sum(item => (long)item.GetRawText().Length) > ContextCharacters)
            throw new BloomException("El contexto de Bloom llegó al límite. Inicia una conversación nueva.");
    }
}

/// <summary>Metadatos sin secretos. Las credenciales se guardan por separado mediante DPAPI.</summary>
public sealed class BloomConfigurationStore(string? path = null)
{
    private readonly string file = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Silen", "bloom-settings.json");
    public BloomConfiguration Load()
    {
        if (!File.Exists(file)) return BloomConfiguration.Default;
        try
        {
            if (new FileInfo(file).Length > 4096) throw new JsonException();
            var config = JsonSerializer.Deserialize<BloomConfiguration>(File.ReadAllText(file)) ?? throw new JsonException();
            config.Validate(); return config;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or BloomException)
        { throw new BloomException("No se pudo cargar la configuración de Bloom. Selecciona de nuevo proveedor y modelo."); }
    }
    public void Save(BloomConfiguration configuration)
    {
        configuration.Validate();
        try { Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, JsonSerializer.Serialize(configuration)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new BloomException("No se pudo guardar la configuración de Bloom."); }
    }
}
