using System.Text.Json;

namespace Silen.Core;

public sealed class BloomConversation(IBloomClient client, IBloomTools tools)
{
    private readonly List<BloomTurn> history = [];
    private readonly Queue<BloomCall> calls = new();
    private readonly List<BloomPart> results = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private BloomAction? pending;
    private BloomCall? pendingCall;
    private int rounds;
    public event Action<string, string>? MessageAdded;
    public BloomUsage Usage { get; private set; } = new();
    public bool HasPending => pending is not null;
    public string PendingDescription => pending is null ? "" : pending.Description + (pendingCall?.Name == "scan_pc" ? "\n\n¿Autorizas recopilar y enviar este resumen al proveedor seleccionado?" : "\n\n¿Deseas que aplique estos ajustes?");

    public Task SendAsync(string key, string text, CancellationToken token = default) => RunAsync(async () =>
    {
        if (HasPending) throw new BloomException("Confirma o cancela la propuesta pendiente antes de continuar.");
        if (string.IsNullOrWhiteSpace(text) || text.Length > BloomLimits.InputCharacters) throw new BloomException("Escribe un mensaje de hasta 4000 caracteres.");
        if (history.Count > 48) throw new BloomException("La conversación llegó a su límite. Inicia una conversación nueva.");
        text = text.Replace(key, "[clave omitida]", StringComparison.Ordinal);
        history.Add(new(BloomRole.User, [new BloomTextPart(text)]));
        MessageAdded?.Invoke("Tú", text); rounds = 0;
        await ContinueAsync(key, token);
    }, token);

    public Task ConfirmAsync(string key, CancellationToken token = default) => RunAsync(async () =>
    {
        if (pending is not { } action || pendingCall is not { } call) return;
        pending = null; pendingCall = null;
        MessageAdded?.Invoke("Tú", "Confirmo esta propuesta.");
        await ExecuteAsync(call, action, key, token);
        await ProcessCallsAsync(key, token);
    }, token);

    public Task CancelAsync(string key, CancellationToken token = default) => RunAsync(async () =>
    {
        if (pendingCall is not { } call) return;
        pending = null; pendingCall = null;
        AddResult(call, new { success = false, cancelled = true, message = "El usuario canceló. No se ejecutó la acción." });
        // Cancela también el resto del lote: nunca propone otra mutación tras pulsar cancelar.
        while (calls.TryDequeue(out var other)) AddResult(other, new { success = false, cancelled = true });
        MessageAdded?.Invoke("Silen", "Propuesta cancelada. No se aplicaron sus cambios.");
        FlushResults();
        await Task.CompletedTask;
    }, token);

    private async Task ContinueAsync(string key, CancellationToken token)
    {
        if (++rounds > 6) throw new BloomException("Bloom alcanzó el límite de acciones de esta respuesta. Los cambios realizados conservan sus respaldos.");
        BloomLimits.ValidateContext(history.Select(BloomMessageAdapter.Serialize).ToArray());
        if (Usage.TotalTokens >= BloomLimits.SessionTokens) throw new BloomException("Se alcanzó el presupuesto de tokens de esta conversación. Inicia una nueva.");
        var reply = await client.SendAsync(key, history.ToArray(), token);
        Usage += reply.TokenUsage ?? new BloomUsage();
        if (Usage.TotalTokens > BloomLimits.SessionTokens) throw new BloomException("La respuesta agotó el presupuesto de tokens; no se ejecutaron nuevas herramientas.");
        var message = reply.Message;
        if (message.Role != BloomRole.Assistant) throw new BloomException("El proveedor devolvió un rol incompatible.");
        var proposedCalls = message.Parts.OfType<BloomCallPart>().Select(part => part.Call).ToArray();
        if (proposedCalls.Length > 4) throw new BloomException("El proveedor excedió el límite de llamadas.");
        history.Add(message);
        var text = string.Join("\n", message.Parts.OfType<BloomTextPart>().Select(part => part.Text)).Replace(key, "[clave omitida]", StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(text)) MessageAdded?.Invoke("Bloom", text);
        foreach (var call in proposedCalls) calls.Enqueue(call);
        if (calls.Count > 0) await ProcessCallsAsync(key, token);
    }
    private async Task ProcessCallsAsync(string key, CancellationToken token)
    {
        while (calls.TryDequeue(out var call))
        {
            token.ThrowIfCancellationRequested();
            if (call.Name is not ("scan_pc" or "apply_optimizations" or "clean_temp_files" or "undo_last_change"))
            { AddResult(call, new { success = false, message = "Herramienta no permitida." }); continue; }
            BloomAction action;
            try { action = await tools.PrepareAsync(call, token); }
            catch (BloomException ex) { AddResult(call, new { success = false, message = "La propuesta no está disponible. El detalle se muestra solo en el equipo del usuario." }); MessageAdded?.Invoke("Silen", ex.Message); continue; }
            pending = action; pendingCall = call;
            MessageAdded?.Invoke("Silen", PendingDescription);
            return;
        }
        FlushResults();
        await ContinueAsync(key, token);
    }
    private async Task ExecuteAsync(BloomCall call, BloomAction action, string key, CancellationToken token)
    {
        var result = await action.ExecuteAsync(token);
        AddResult(call, BloomResultPrivacy.ForProvider(call.Name, result));
        if (result.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            MessageAdded?.Invoke("Silen", (message.GetString() ?? "").Replace(key, "[clave omitida]", StringComparison.Ordinal));
    }
    private void AddResult(BloomCall call, object result)
    {
        results.Add(new BloomResultPart(call.Name, JsonSerializer.SerializeToElement(result), call.Id));
    }
    private void FlushResults()
    {
        if (results.Count == 0) return;
        history.Add(new(BloomRole.User, results.ToArray()));
        results.Clear();
    }
    private async Task RunAsync(Func<Task> action, CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token)) throw new BloomException("Bloom ya está atendiendo una solicitud.");
        try { await action(); }
        catch
        {
            // No repetir un lote incompleto al reintentar, especialmente si ya hubo una mutación.
            pending = null; pendingCall = null; calls.Clear(); results.Clear(); history.Clear();
            throw;
        }
        finally { gate.Release(); }
    }
}



