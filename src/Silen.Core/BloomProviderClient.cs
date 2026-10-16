using System.Net;
using System.Text;
using System.Text.Json;

namespace Silen.Core;

/// <summary>Adapta los protocolos a partes de texto, llamadas y resultados locales normalizados.</summary>
public sealed class BloomProviderClient : IBloomClient
{
    private static readonly HttpClient Shared = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly BloomConfiguration configuration;
    private readonly HttpClient http;
    private readonly TimeSpan deadline;
    public BloomProviderClient(BloomConfiguration configuration, HttpClient? http = null, TimeSpan? deadline = null)
    {
        configuration.Validate(); this.configuration = configuration; this.http = http ?? Shared; this.deadline = deadline ?? TimeSpan.FromSeconds(90);
    }
    public string ModelLabel => configuration.Provider + " · " + configuration.Model;
    public Task<BloomReply> SendAsync(string key, IReadOnlyList<BloomTurn> messages, CancellationToken token = default) =>
        SendAsync(key, messages.Select(BloomMessageAdapter.Serialize).ToArray(), token);
    public async Task<BloomReply> SendAsync(string key, IReadOnlyList<JsonElement> contents, CancellationToken token = default)
    {
        BloomGeminiClient.ValidateKey(key); BloomLimits.ValidateContext(contents);
        foreach (var message in contents) _ = BloomMessageAdapter.Parse(message);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(deadline);
        try
        {
            using var request = CreateRequest(key, contents);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new BloomException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "El proveedor rechazó la clave o sus permisos. Revisa la configuración de Bloom.",
                HttpStatusCode.TooManyRequests => "Se alcanzó la cuota del proveedor. Espera antes de volver a enviar un mensaje.",
                HttpStatusCode.NotFound => $"El proveedor no permite acceder al modelo {configuration.Model}. Selecciona otro modelo disponible para tu cuenta.",
                _ => "El proveedor no completó la solicitud. No se repitió ninguna acción."
            });
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var memory = new MemoryStream(); var buffer = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (memory.Length + read > BloomLimits.ResponseBytes) throw new BloomException("La respuesta de Bloom excedió el límite de tamaño.");
                memory.Write(buffer, 0, read);
            }
            using var document = JsonDocument.Parse(memory.ToArray());
            var reply = Parse(document.RootElement);
            if (reply.Message.Role != BloomRole.Assistant) throw new BloomException("El proveedor devolvió un rol incompatible.");
            if (reply.Calls.Count > 4 || reply.Text.Length > 24000 || reply.TokenUsage is { InputTokens: < 0 } or { OutputTokens: < 0 })
                throw new BloomException("El proveedor devolvió una respuesta incompatible.");
            return reply;
        }
        catch (BloomException) { throw; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { throw new BloomException("Bloom tardó demasiado en responder. No se repitió ninguna acción."); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or IndexOutOfRangeException or ArgumentException)
        { throw new BloomException("No se pudo completar la conexión o interpretar la respuesta del proveedor."); }
    }
    private HttpRequestMessage CreateRequest(string key, IReadOnlyList<JsonElement> contents)
    {
        var endpoint = configuration.Provider switch
        {
            BloomProvider.Gemini => "https://generativelanguage.googleapis.com/v1beta/models/" + configuration.Model + ":generateContent",
            BloomProvider.OpenAI => "https://api.openai.com/v1/chat/completions",
            BloomProvider.Anthropic => "https://api.anthropic.com/v1/messages",
            _ => throw new BloomException("Proveedor no admitido.")
        };
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        object body;
        if (configuration.Provider == BloomProvider.Gemini)
        {
            request.Headers.Add("x-goog-api-key", key);
            body = new { contents, systemInstruction = new { parts = new[] { new { text = BloomToolCatalog.Instruction } } }, tools = BloomToolCatalog.Tools, generationConfig = new { maxOutputTokens = BloomLimits.OutputTokens } };
        }
        else
        {
            var declarations = BloomToolCatalog.Tools[0].GetProperty("functionDeclarations").EnumerateArray().ToArray();
            if (configuration.Provider == BloomProvider.OpenAI)
            {
                request.Headers.Authorization = new("Bearer", key);
                var messages = OpenAiMessages(contents); messages.Insert(0, new { role = "system", content = BloomToolCatalog.Instruction });
                body = new { model = configuration.Model, messages, max_completion_tokens = BloomLimits.OutputTokens, store = false,
                    tools = declarations.Select(tool => new { type = "function", function = new { name = tool.GetProperty("name").GetString(), description = tool.GetProperty("description").GetString(), parameters = Schema(tool) } }) };
            }
            else
            {
                request.Headers.Add("x-api-key", key); request.Headers.Add("anthropic-version", "2023-06-01");
                body = new { model = configuration.Model, system = BloomToolCatalog.Instruction, max_tokens = BloomLimits.OutputTokens,
                    messages = AnthropicMessages(contents), tools = declarations.Select(tool => new { name = tool.GetProperty("name").GetString(), description = tool.GetProperty("description").GetString(), input_schema = Schema(tool) }) };
            }
        }
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"); return request;
    }
    private static JsonElement Schema(JsonElement tool)
    {
        var schema = tool.GetProperty("parameters").GetRawText();
        foreach (var type in new[] { "OBJECT", "ARRAY", "STRING", "BOOLEAN" }) schema = schema.Replace('"' + type + '"', '"' + type.ToLowerInvariant() + '"', StringComparison.Ordinal);
        return JsonDocument.Parse(schema).RootElement.Clone();
    }
    private static List<object> OpenAiMessages(IReadOnlyList<JsonElement> contents)
    {
        List<object> messages = [];
        foreach (var turn in contents)
        {
            var parts = turn.GetProperty("parts").EnumerateArray().ToArray();
            var responses = parts.Where(part => part.TryGetProperty("functionResponse", out _)).ToArray();
            if (responses.Length > 0)
            {
                foreach (var part in responses) { var result = part.GetProperty("functionResponse"); messages.Add(new { role = "tool", tool_call_id = result.GetProperty("id").GetString(), content = result.GetProperty("response").GetRawText() }); }
                continue;
            }
            var text = string.Join("\n", parts.Where(part => part.TryGetProperty("text", out _)).Select(part => part.GetProperty("text").GetString()));
            if (turn.GetProperty("role").GetString() == "user") messages.Add(new { role = "user", content = text });
            else
            {
                var calls = parts.Where(part => part.TryGetProperty("functionCall", out _)).Select(part => part.GetProperty("functionCall")).ToArray();
                messages.Add(new { role = "assistant", content = string.IsNullOrEmpty(text) ? null : text,
                    tool_calls = calls.Length == 0 ? null : calls.Select(call => new { id = call.GetProperty("id").GetString(), type = "function", function = new { name = call.GetProperty("name").GetString(), arguments = call.GetProperty("args").GetRawText() } }).ToArray() });
            }
        }
        return messages;
    }
    private static List<object> AnthropicMessages(IReadOnlyList<JsonElement> contents)
    {
        List<object> messages = [];
        foreach (var turn in contents)
        {
            List<object> blocks = [];
            foreach (var part in turn.GetProperty("parts").EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text)) blocks.Add(new { type = "text", text = text.GetString() });
                else if (part.TryGetProperty("functionCall", out var call)) blocks.Add(new { type = "tool_use", id = call.GetProperty("id").GetString(), name = call.GetProperty("name").GetString(), input = call.GetProperty("args") });
                else if (part.TryGetProperty("functionResponse", out var result)) blocks.Add(new { type = "tool_result", tool_use_id = result.GetProperty("id").GetString(), content = result.GetProperty("response").GetRawText() });
            }
            messages.Add(new { role = turn.GetProperty("role").GetString() == "model" ? "assistant" : "user", content = blocks });
        }
        return messages;
    }
    private BloomReply Parse(JsonElement root)
    {
        if (configuration.Provider == BloomProvider.Gemini)
        {
            var candidate = root.GetProperty("candidates")[0];
            if (candidate.GetProperty("finishReason").GetString() != "STOP") throw new BloomException("El modelo no completó una respuesta utilizable.");
            var metadata = root.TryGetProperty("usageMetadata", out var usage) ? usage : default;
            return new(candidate.GetProperty("content").Clone(), new(Count(metadata, "promptTokenCount"), BloomUsage.AddCounts(Count(metadata, "candidatesTokenCount"), Count(metadata, "thoughtsTokenCount"))));
        }
        List<object> parts = []; BloomUsage tokens;
        if (configuration.Provider == BloomProvider.OpenAI)
        {
            var choice = root.GetProperty("choices")[0]; var finish = choice.GetProperty("finish_reason").GetString();
            if (finish is not ("stop" or "tool_calls")) throw new BloomException("El modelo no completó una respuesta utilizable.");
            var message = choice.GetProperty("message");
            if (message.TryGetProperty("content", out var text) && text.ValueKind == JsonValueKind.String) parts.Add(new { text = text.GetString() });
            if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
                foreach (var call in calls.EnumerateArray()) { var function = call.GetProperty("function"); using var args = JsonDocument.Parse(function.GetProperty("arguments").GetString()!); parts.Add(new { functionCall = new { id = call.GetProperty("id").GetString(), name = function.GetProperty("name").GetString(), args = args.RootElement.Clone() } }); }
            var usage = root.TryGetProperty("usage", out var value) ? value : default; tokens = new(Count(usage, "prompt_tokens"), Count(usage, "completion_tokens"));
        }
        else
        {
            if (root.GetProperty("stop_reason").GetString() is not ("end_turn" or "tool_use")) throw new BloomException("El modelo no completó una respuesta utilizable.");
            foreach (var block in root.GetProperty("content").EnumerateArray())
            {
                if (block.GetProperty("type").GetString() == "text") parts.Add(new { text = block.GetProperty("text").GetString() });
                else if (block.GetProperty("type").GetString() == "tool_use") parts.Add(new { functionCall = new { id = block.GetProperty("id").GetString(), name = block.GetProperty("name").GetString(), args = block.GetProperty("input").Clone() } });
            }
            var usage = root.TryGetProperty("usage", out var value) ? value : default;
            tokens = new(BloomUsage.AddCounts(BloomUsage.AddCounts(Count(usage, "input_tokens"), Count(usage, "cache_creation_input_tokens")), Count(usage, "cache_read_input_tokens")), Count(usage, "output_tokens"));
        }
        return new(JsonSerializer.SerializeToElement(new { role = "model", parts }), tokens);
    }
    internal static long Count(JsonElement usage, string property)
    {
        var value = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty(property, out var count) ? count.GetInt64() : 0;
        if (value < 0) throw new BloomException("El proveedor devolvió un consumo de tokens inválido.");
        return value;
    }
}

