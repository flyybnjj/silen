using System.Globalization;
using System.Text.Json;
using Silen.Core;

namespace Silen.Windows;

public sealed class BloomWindowsTools(INativeOptimizationService native,
    Func<CancellationToken, Task<SystemSnapshot>> collect) : IBloomTools
{
    private static readonly Dictionary<string, string[]> Categories = new(StringComparer.Ordinal)
    {
        ["red"] = ["delivery-optimization", "gaming-network-bandwidth"],
        ["gaming"] = ["game-mode", "xbox-captures", "gamebar-background"],
        ["servicios"] = ["service-diagtrack", "service-maps", "service-fax"]
    };
    public async Task<BloomAction> PrepareAsync(BloomCall call, CancellationToken token)
    {
        if (call.Arguments.ValueKind != JsonValueKind.Object) throw new BloomException("Bloom propuso parámetros incompatibles. No se cambió Windows.");
        if (call.Name != "apply_optimizations" && call.Arguments.EnumerateObject().Any()) throw new BloomException("Esta herramienta no acepta parámetros adicionales.");
        switch (call.Name)
        {
            case "scan_pc":
                return new("Recopilar y enviar al proveedor seleccionado un resumen de uso de CPU, RAM y GPU, temperaturas, espacio bajo, recuento de problemas de drivers y estado de Windows Update. No incluye nombres de procesos, PID, comandos, etiquetas de discos ni identificadores del equipo. El texto que escribas también se envía al proveedor.", async cancellation =>
                {
                    var snapshot = await collect(cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    return JsonSerializer.SerializeToElement(new { success = true, message = "Diagnóstico terminado. Se comparte solo el resumen agregado autorizado.", summary = BloomDiagnosticSummary.From(snapshot) }, ReportStore.JsonOptions);
                });
            case "apply_optimizations":
                return await PrepareOptimizationAsync(call.Arguments, token);
            case "clean_temp_files":
                var local = native is SystemActionOptimizationService proxy ? proxy.Local : native as NativeOptimizationService;
                var batch = local?.CreateIndependentCleanup();
                var preview = batch is null ? await native.PreviewTempAsync(token) : await batch.PreviewAsync(token);
                if (preview.Files.Count == 0) throw new BloomException("No hay temporales elegibles en este lote. No se eliminó ningún archivo.");
                var ids = preview.Files.Select(file => file.Id).ToArray();
                var size = (preview.TotalBytes / 1048576d).ToString("N1", CultureInfo.GetCultureInfo("es-CL"));
                return new($"Retirar {SpanishText.Count(ids.Length, "temporal", "temporales")} de al menos 24 horas ({size} MiB) hacia recuperación local. Se omiten archivos cambiados, enlaces y archivos en uso. Puedes restaurar el lote desde Ajustes; el espacio solo se libera al purgar con confirmación." +
                    (preview.Truncated ? " Es un lote acotado; puede quedar contenido pendiente." : ""), async cancellation =>
                {
                    var result = batch is null ? await native.CleanTempAsync(ids, cancellation) : await batch.CleanAsync(ids, cancellation);
                    return JsonSerializer.SerializeToElement(new { success = result.Removed > 0 && !cancellation.IsCancellationRequested, message = $"{result.Message} {result.RecoverableBytes} bytes retirados recuperables; {result.BytesFreed} bytes liberados, {result.Removed} retirados y {result.Skipped} omitidos.", result.Removed, result.Skipped, result.BytesFreed, result.RecoverableBytes });
                });
            case "undo_last_change":
                var changes = await native.InspectAsync(token);
                var latest = changes.Changes.Where(change => !change.Restored).OrderByDescending(change => change.AppliedAt).FirstOrDefault()
                    ?? throw new BloomException("No hay cambios respaldados pendientes para restaurar.");
                if (!latest.CanUndo) throw new BloomException("El último respaldo no se puede restaurar ahora: " + latest.Detail);
                return new("Restaurar el último cambio respaldado: " + latest.Title + ". Se comprueba que otro programa no haya cambiado esos valores.", async cancellation =>
                {
                    var result = await native.UndoAsync(latest.Id, cancellation);
                    return JsonSerializer.SerializeToElement(new { success = result.Success, message = result.Message, changed = result.Changed, verification = result.Verification.ToString(), mayHaveChanged = result.MayHaveChanged });
                });
            default: throw new BloomException("Herramienta fuera del catálogo de Bloom.");
        }
    }
    private async Task<BloomAction> PrepareOptimizationAsync(JsonElement args, CancellationToken token)
    {
        if (args.EnumerateObject().Any(property => property.Name is not ("categories" or "preserve_audio")) ||
            !args.TryGetProperty("preserve_audio", out var audio) || audio.ValueKind != JsonValueKind.True ||
            !args.TryGetProperty("categories", out var groups) || groups.ValueKind != JsonValueKind.Array || groups.GetArrayLength() is < 1 or > 3)
            throw new BloomException("La propuesta necesita categorías válidas y debe conservar el audio. No se cambió Windows.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groups.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.String || !Categories.TryGetValue(group.GetString()!, out var allowed))
                throw new BloomException("Solo se permiten las categorías red, gaming y servicios.");
            ids.UnionWith(allowed);
        }
        // Una instancia por propuesta: otra vista no puede sustituir la selección que el usuario revisó.
        var pc = new PcOptimizationService(native);
        var preview = await pc.PreviewAsync(token);
        var selected = preview.Options.Where(option => ids.Contains(option.Id) && option.Available && !option.AlreadyApplied).ToArray();
        var omitted = preview.Options.Where(option => ids.Contains(option.Id) && !option.Available).ToArray();
        if (selected.Length == 0) throw new BloomException("No hay cambios nuevos disponibles para esa categoría. Revisa permisos y estado en Ajustes; lo que ya estaba aplicado se conserva.");
        var selectedIds = selected.Select(option => option.Id).ToArray();
        var description = "Aplicar estos pasos, con respaldo de cada estado original:\n" +
            string.Join("\n\n", selected.Select(option => "• " + option.Title + ": " + option.Description)) +
            "\n\nSe conservan los servicios de audio. Los cambios de servicios pueden afectar mapas, fax y diagnósticos; los de capturas afectan grabación de Game Bar. No se garantiza menor ping ni más FPS.";
        if (omitted.Length > 0) description += "\nNo se aplicarán: " + string.Join("; ", omitted.Select(option => option.Title + " (" + option.Status + ")"));
        return new(description, async cancellation =>
        {
            var result = await pc.ApplyAsync(selectedIds, cancellationToken: cancellation);
            return JsonSerializer.SerializeToElement(new { success = !result.Cancelled && result.Failed == 0 && result.Skipped == 0, message = result.Message, result.Steps }, ReportStore.JsonOptions);
        });
    }
}
