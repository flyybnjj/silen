using System.Net;
using System.Text;
using System.Text.Json;

namespace Silen.Core;

public sealed class BloomGeminiClient(HttpClient? http = null, TimeSpan? deadline = null) : IBloomClient
{
    private static readonly HttpClient Shared = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(90) };
    private readonly HttpClient client = http ?? Shared;
    public const string ModelId = "gemini-3.5-flash-lite";
    public string ModelLabel => "Gemini 3.5 Flash-Lite";
    public const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/" + ModelId + ":generateContent";
    public async Task<BloomReply> SendAsync(string key, IReadOnlyList<JsonElement> contents, CancellationToken token = default)
    {
        ValidateKey(key);
        BloomLimits.ValidateContext(contents);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(deadline ?? TimeSpan.FromSeconds(90));
        var requestToken = timeout.Token;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Add("x-goog-api-key", key);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                contents, systemInstruction = new { parts = new[] { new { text = BloomToolCatalog.Instruction } } }, tools = BloomToolCatalog.Tools,
                toolConfig = new { functionCallingConfig = new { mode = "AUTO" } },
                generationConfig = new { maxOutputTokens = 2048 }
            }), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken);
            if (!response.IsSuccessStatusCode) throw new BloomException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Google rechazó la clave o los permisos. Revisa tu clave en AI Studio.",
                HttpStatusCode.TooManyRequests => "Se alcanzó la cuota de Gemini. Espera antes de volver a enviar un mensaje.",
                HttpStatusCode.BadRequest => "Gemini rechazó la solicitud. Revisa que la clave y el modelo estén disponibles para tu proyecto.",
                HttpStatusCode.NotFound => $"Google no permite acceder a {ModelId} con este proyecto. La clave puede ser válida aunque el modelo esté restringido. Revisa su disponibilidad en AI Studio.",
                _ => "Gemini no está disponible en este momento. No se repitió ninguna acción."
            });
            await using var stream = await response.Content.ReadAsStreamAsync(requestToken);
            using var memory = new MemoryStream();
            var buffer = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(buffer, requestToken)) > 0)
            {
                if (memory.Length + read > 2 * 1024 * 1024) throw new BloomException("Gemini devolvió una respuesta demasiado grande.");
                memory.Write(buffer, 0, read);
            }
            using var doc = JsonDocument.Parse(memory.ToArray());
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0 || candidates[0].GetProperty("finishReason").GetString() != "STOP")
                throw new BloomException("Gemini no completó una respuesta utilizable. No se ejecutaron nuevas herramientas.");
            var content = candidates[0].GetProperty("content").Clone();
            var usage = doc.RootElement.TryGetProperty("usageMetadata", out var metadata)
                ? new BloomUsage(BloomProviderClient.Count(metadata, "promptTokenCount"), BloomUsage.AddCounts(BloomProviderClient.Count(metadata, "candidatesTokenCount"), BloomProviderClient.Count(metadata, "thoughtsTokenCount"))) : new BloomUsage();
            var reply = new BloomReply(content, usage);
            if (content.GetProperty("role").GetString() != "model" || reply.Calls.Count > 4 || reply.Text.Length > 24000)
                throw new BloomException("Gemini devolvió una respuesta incompatible.");
            return reply;
        }
        catch (BloomException) { throw; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { throw new BloomException("Gemini tardó demasiado en responder. No se repitió ninguna acción."); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or IndexOutOfRangeException or ArgumentException)
        { throw new BloomException("No se pudo completar la conexión o interpretar la respuesta de Gemini."); }
    }
    internal static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 512 || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl))
            throw new BloomException("Introduce una clave válida del proveedor seleccionado, sin espacios.");
    }
}

