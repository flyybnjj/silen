using System.Text.Json;

namespace Silen.Core;

public enum BloomRole { User, Assistant }
public abstract record BloomPart;
public sealed record BloomTextPart(string Text) : BloomPart;
public sealed record BloomCallPart(BloomCall Call) : BloomPart;
public sealed record BloomResultPart(string Name, JsonElement Result, string? CallId = null) : BloomPart;

/// <summary>Mensaje independiente del proveedor. La UI no recibe metadatos criptográficos del protocolo.</summary>
public sealed record BloomTurn(BloomRole Role, IReadOnlyList<BloomPart> Parts)
{
    // Las firmas de Gemini solo se conservan para su adaptador; no son instrucciones ni contenido de UI.
    internal JsonElement? GeminiContent { get; init; }
}

/// <summary>Puente de compatibilidad para clientes y pruebas anteriores; valida antes de adaptar.</summary>
internal static class BloomMessageAdapter
{
    internal static BloomTurn Parse(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("role", out var role) ||
            role.ValueKind != JsonValueKind.String || role.GetString() is not ("user" or "model") ||
            !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() is < 1 or > 16)
            throw new BloomException("El proveedor devolvió un mensaje incompatible.");
        List<BloomPart> normalized = [];
        var assistant = role.GetString() == "model";
        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object) throw new BloomException("Parte de Bloom incompatible.");
            var kindCount = (part.TryGetProperty("text", out var text) ? 1 : 0) +
                (part.TryGetProperty("functionCall", out var call) ? 1 : 0) + (part.TryGetProperty("functionResponse", out var response) ? 1 : 0);
            if (kindCount != 1) throw new BloomException("Parte de Bloom ambigua o incompatible.");
            if (text.ValueKind != JsonValueKind.Undefined)
            {
                if (text.ValueKind != JsonValueKind.String || text.GetString()!.Length > 24000) throw new BloomException("Texto de Bloom incompatible.");
                if (!part.TryGetProperty("thought", out var thought) || thought.ValueKind != JsonValueKind.True)
                    normalized.Add(new BloomTextPart(text.GetString()!));
            }
            else if (call.ValueKind != JsonValueKind.Undefined)
            {
                if (!assistant) throw new BloomException("Llamada de herramienta en un rol incompatible.");
                var name = Name(call); var args = call.TryGetProperty("args", out var arguments) ? arguments : JsonSerializer.SerializeToElement(new { });
                if (args.ValueKind != JsonValueKind.Object) throw new BloomException("Parámetros de herramienta incompatibles.");
                normalized.Add(new BloomCallPart(new(name, args.Clone(), Id(call))));
            }
            else
            {
                if (assistant || response.ValueKind != JsonValueKind.Object || !response.TryGetProperty("response", out var result) || result.ValueKind != JsonValueKind.Object)
                    throw new BloomException("Resultado de herramienta incompatible.");
                normalized.Add(new BloomResultPart(Name(response), result.Clone(), Id(response)));
            }
        }
        return new(assistant ? BloomRole.Assistant : BloomRole.User, normalized) { GeminiContent = content.Clone() };
    }
    private static string Name(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String ||
            name.GetString() is not { Length: > 0 and <= 128 } nameText || nameText.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_')))
            throw new BloomException("Nombre de herramienta incompatible.");
        return nameText;
    }
    private static string? Id(JsonElement value)
    {
        if (!value.TryGetProperty("id", out var id)) return null;
        if (id.ValueKind != JsonValueKind.String || id.GetString() is not { Length: > 0 and <= 256 } result || result.Any(char.IsControl))
            throw new BloomException("Identificador de llamada incompatible.");
        return result;
    }
    internal static JsonElement Serialize(BloomTurn turn)
    {
        if (turn.GeminiContent is { } original) return original.Clone();
        return JsonSerializer.SerializeToElement(new
        {
            role = turn.Role == BloomRole.Assistant ? "model" : "user",
            parts = turn.Parts.Select<BloomPart, object>(part => part switch
            {
                BloomTextPart text => new { text = text.Text },
                BloomCallPart call => new { functionCall = new { name = call.Call.Name, args = call.Call.Arguments, id = call.Call.Id } },
                BloomResultPart result => new { functionResponse = Result(result) },
                _ => throw new BloomException("Parte de Bloom no admitida.")
            })
        });
    }
    private static Dictionary<string, object> Result(BloomResultPart part)
    {
        Dictionary<string, object> result = new() { ["name"] = part.Name, ["response"] = part.Result };
        if (part.CallId is not null) result["id"] = part.CallId; return result;
    }
}
