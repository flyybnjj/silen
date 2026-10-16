using System.Text.Json;

namespace Silen.Core;

public sealed class BloomException(string message) : Exception(message);
public sealed record BloomCall(string Name, JsonElement Arguments, string? Id = null);
public sealed record BloomAction(string Description, Func<CancellationToken, Task<JsonElement>> ExecuteAsync);
public interface IBloomTools
{
    Task<BloomAction> PrepareAsync(BloomCall call, CancellationToken token);
}
public interface IBloomClient
{
    string ModelLabel => "Gemini";
    Task<BloomReply> SendAsync(string key, IReadOnlyList<BloomTurn> messages, CancellationToken token = default) =>
        SendAsync(key, messages.Select(BloomMessageAdapter.Serialize).ToArray(), token);
    // Compatibilidad con los clientes inyectados previos. La conversación usa el contrato tipado.
    Task<BloomReply> SendAsync(string key, IReadOnlyList<JsonElement> contents, CancellationToken token = default);
}
public interface IBloomKeyStore
{
    string? Read();
    void Save(string key);
    void Delete();
}
public sealed record BloomReply(JsonElement Content, BloomUsage? TokenUsage = null)
{
    public BloomTurn Message => BloomMessageAdapter.Parse(Content);
    public string Text => string.Join("\n", Content.GetProperty("parts").EnumerateArray()
        .Where(part => !part.TryGetProperty("thought", out var thought) || thought.ValueKind != JsonValueKind.True)
        .Where(part => part.TryGetProperty("text", out _)).Select(part => part.GetProperty("text").GetString()));
    public IReadOnlyList<BloomCall> Calls => Content.GetProperty("parts").EnumerateArray()
        .Where(part => part.TryGetProperty("functionCall", out _)).Select(part =>
        {
            var call = part.GetProperty("functionCall");
            return new BloomCall(call.GetProperty("name").GetString() ?? "", call.TryGetProperty("args", out var args) ? args.Clone() : JsonSerializer.SerializeToElement(new { }),
                call.TryGetProperty("id", out var id) ? id.GetString() : null);
        }).ToArray();
}

