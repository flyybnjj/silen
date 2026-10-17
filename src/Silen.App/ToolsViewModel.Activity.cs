using Silen.Core;
using Silen.Windows;

namespace Silen.App;

public sealed partial class ToolsViewModel
{
    internal event Action<string, string>? ActivityProgress;
    internal event Action<string, string>? ActivityResult;

    private async Task<TemporaryCleanupResult> CleanWithProgressAsync(string[] ids, CancellationToken token)
    {
        ActivityProgress?.Invoke("Estoy retirando temporales.", $"Temporales de tu usuario · 0 de {ids.Length} revisados. Se conservan para recuperación.");
        // El despachador puede dibujar la respuesta antes de iniciar E/S local.
        await Task.Yield();
        var active = true;
        using var progress = new LatestCleanupProgress(value =>
        {
            if (!active) return;
            ActivityProgress?.Invoke("Estoy retirando temporales.", $"Temporales de tu usuario · {value.Processed} de {value.Total} revisados · {SpanishText.Count(value.Removed, "archivo retirado", "archivos retirados")}.");
        });
        try
        {
            var result = await service.CleanTempAsync(ids, progress, token);
            ActivityResult?.Invoke(token.IsCancellationRequested ? "He detenido la limpieza." : "He terminado esta tanda.",
                $"Temporales de tu usuario · {SpanishText.Count(result.Removed, "archivo retirado", "archivos retirados")} · {SpanishText.Count(result.Skipped, "sin retirar", "sin retirar")}. En recuperación: {Size(result.RecoverableBytes)}. Espacio liberado: {Size(result.BytesFreed)}.");
            return result;
        }
        finally { active = false; }
    }
}

/// <summary>Conserva solo la lectura más reciente; no encola un repintado por archivo.</summary>
internal sealed class LatestCleanupProgress(Action<TemporaryCleanupProgress> deliver) : IProgress<TemporaryCleanupProgress>, IDisposable
{
    private readonly SynchronizationContext? context = SynchronizationContext.Current;
    private readonly object gate = new();
    private TemporaryCleanupProgress? latest;
    private bool scheduled, disposed;
    public void Report(TemporaryCleanupProgress value)
    {
        lock (gate)
        {
            if (disposed) return;
            latest = value;
            if (scheduled) return;
            scheduled = true;
        }
        if (context is null) Drain(); else context.Post(_ => Drain(), null);
    }
    private void Drain()
    {
        TemporaryCleanupProgress? value;
        lock (gate) { value = latest; scheduled = false; if (disposed) return; }
        if (value is not null) deliver(value);
    }
    public void Dispose() { lock (gate) { disposed = true; latest = null; } }
}
