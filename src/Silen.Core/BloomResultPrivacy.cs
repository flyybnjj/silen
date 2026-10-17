using System.Text.Json;

namespace Silen.Core;

// Frontera de salida: los mensajes locales, títulos y rutas nunca se convierten
// en contexto remoto. Solo pasan campos tipados de las cuatro herramientas.
internal static class BloomResultPrivacy
{
    internal static JsonElement ForProvider(string tool, JsonElement local)
    {
        var result = new Dictionary<string, object?>();
        Copy(local, result, "success", boolean: true);
        result["message"] = "Resultado local de Silen. Los detalles técnicos permanecen en el equipo; usa solo los estados y contadores recibidos.";
        switch (tool)
        {
            case "scan_pc":
                if (local.TryGetProperty("summary", out var summary))
                    result["summary"] = summary.Deserialize<BloomDiagnosticSummary>();
                break;
            case "undo_last_change":
                Copy(local, result, "changed", boolean: true);
                Copy(local, result, "mayHaveChanged", boolean: true);
                CopyVerification(local, result, "verification");
                break;
            case "clean_temp_files":
                foreach (var name in new[] { "Removed", "Skipped", "BytesFreed", "RecoverableBytes" }) Copy(local, result, name, boolean: false);
                break;
            case "apply_optimizations":
                if (local.TryGetProperty("Steps", out var steps))
                {
                    if (steps.ValueKind != JsonValueKind.Array || steps.GetArrayLength() > 64) throw new BloomException("Resultado local incompatible; no se envió al proveedor.");
                    result["Steps"] = steps.EnumerateArray().Select(step =>
                    {
                        var state = new Dictionary<string, object?>();
                        foreach (var name in new[] { "Success", "Changed", "Skipped", "MayHaveChanged" }) Copy(step, state, name, boolean: true);
                        CopyVerification(step, state, "Verification");
                        return state;
                    }).ToArray();
                }
                break;
            default: throw new BloomException("Resultado de una herramienta fuera del catálogo.");
        }
        return JsonSerializer.SerializeToElement(result);
    }

    private static void Copy(JsonElement source, Dictionary<string, object?> target, string name, bool boolean)
    {
        if (!source.TryGetProperty(name, out var value)) return;
        if (boolean && value.ValueKind is JsonValueKind.True or JsonValueKind.False) target[name] = value.GetBoolean();
        else if (!boolean && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number >= 0) target[name] = number;
        else throw new BloomException("Estado o contador local incompatible; no se envió al proveedor.");
    }

    private static void CopyVerification(JsonElement source, Dictionary<string, object?> target, string name)
    {
        if (!source.TryGetProperty(name, out var value)) return;
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not ("Verified" or "Uncertain"))
            throw new BloomException("Verificación local incompatible; no se envió al proveedor.");
        target[name] = value.GetString();
    }
}
