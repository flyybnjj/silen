using System.Net;
using System.Text.Json;
using Silen.Core;
using Xunit;

namespace Silen.Tests;

public sealed class BloomProviderTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();
    [Theory]
    [InlineData(BloomProvider.Gemini, "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent", "x-goog-api-key")]
    [InlineData(BloomProvider.OpenAI, "https://api.openai.com/v1/chat/completions", "Authorization")]
    [InlineData(BloomProvider.Anthropic, "https://api.anthropic.com/v1/messages", "x-api-key")]
    public async Task Providers_use_fixed_https_normalize_calls_results_and_report_usage(BloomProvider provider, string endpoint, string header)
    {
        var requestCount = 0;
        var handler = new Handler(async (request, token) =>
        {
            Assert.Equal(endpoint, request.RequestUri!.AbsoluteUri); Assert.True(request.Headers.Contains(header));
            var body = await request.Content!.ReadAsStringAsync(token); Assert.DoesNotContain("fake-key", body);
            if (++requestCount == 2) Assert.Contains("prueba-confirmada", body);
            var fixture = provider switch
            {
                BloomProvider.Gemini => """{"candidates":[{"finishReason":"STOP","content":{"role":"model","parts":[{"functionCall":{"name":"scan_pc","args":{},"id":"a"}}]}}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":3}}""",
                BloomProvider.OpenAI => """{"choices":[{"finish_reason":"tool_calls","message":{"role":"assistant","content":null,"tool_calls":[{"id":"a","type":"function","function":{"name":"scan_pc","arguments":"{}"}}]}}],"usage":{"prompt_tokens":10,"completion_tokens":3}}""",
                _ => """{"stop_reason":"tool_use","content":[{"type":"tool_use","id":"a","name":"scan_pc","input":{}}],"usage":{"input_tokens":10,"output_tokens":3}}"""
            };
            return new(HttpStatusCode.OK) { Content = new StringContent(fixture) };
        });
        var client = new BloomProviderClient(new(provider, BloomConfiguration.DefaultModel(provider)), new HttpClient(handler));
        var input = Json("""{"role":"user","parts":[{"text":"hola"}]}""");
        var reply = await client.SendAsync("fake-key", [input]);
        Assert.Equal("scan_pc", reply.Calls.Single().Name); Assert.Equal(13, reply.TokenUsage!.TotalTokens);
        await client.SendAsync("fake-key", [input, reply.Content, Json("""{"role":"user","parts":[{"functionResponse":{"id":"a","name":"scan_pc","response":{"message":"prueba-confirmada"}}}]}""")]);
    }
    [Theory]
    [InlineData(BloomProvider.Gemini)] [InlineData(BloomProvider.OpenAI)] [InlineData(BloomProvider.Anthropic)]
    public async Task Deadline_includes_slow_response_body_after_headers(BloomProvider provider)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new SlowStream()) }));
        var client = new BloomProviderClient(new(provider, BloomConfiguration.DefaultModel(provider)), new HttpClient(handler), TimeSpan.FromMilliseconds(40));
        var task = client.SendAsync("fake-key", [Json("""{"role":"user","parts":[{"text":"hola"}]}""")]);
        var error = await Assert.ThrowsAsync<BloomException>(() => task.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Contains("demasiado", error.Message);
    }
    [Fact]
    public async Task Gemini_legacy_client_also_bounds_response_body()
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new SlowStream()) }));
        var client = new BloomGeminiClient(new HttpClient(handler), TimeSpan.FromMilliseconds(40));
        await Assert.ThrowsAsync<BloomException>(() => client.SendAsync("fake-key", []).WaitAsync(TimeSpan.FromSeconds(3)));
    }
    [Fact]
    public async Task Oversized_context_is_rejected_before_network()
    {
        var sent = false;
        var client = new BloomProviderClient(BloomConfiguration.Default, new HttpClient(new Handler((_, _) => { sent = true; throw new Exception(); })));
        await Assert.ThrowsAsync<BloomException>(() => client.SendAsync("fake-key", [JsonSerializer.SerializeToElement(new { text = new string('a', 64001) })]));
        Assert.False(sent);
    }
    [Fact]
    public void Custom_model_or_url_cannot_override_allowlist()
    {
        Assert.Throws<BloomException>(() => new BloomProviderClient(new(BloomProvider.OpenAI, "https://attacker.invalid")));
        Assert.Throws<BloomException>(() => new BloomProviderClient(new((BloomProvider)123, "model")));
    }
    [Fact]
    public async Task Session_token_budget_blocks_new_tools_and_further_requests()
    {
        var tools = new Tools(); var client = new BudgetClient(); var chat = new BloomConversation(client, tools);
        await Assert.ThrowsAsync<BloomException>(() => chat.SendAsync("fake-key", "hola"));
        Assert.Equal(0, tools.Prepared); Assert.False(chat.HasPending);
        await Assert.ThrowsAsync<BloomException>(() => chat.SendAsync("fake-key", "otra vez"));
        Assert.Equal(1, client.Calls);
    }
    [Fact]
    public void Extreme_usage_cannot_wrap_the_total_or_accumulated_budget()
    {
        Assert.Equal(long.MaxValue, new BloomUsage(long.MaxValue, 1).TotalTokens);
        var accumulated = new BloomUsage(long.MaxValue, 1) + new BloomUsage(1, long.MaxValue);
        Assert.Equal(long.MaxValue, accumulated.InputTokens);
        Assert.Equal(long.MaxValue, accumulated.OutputTokens);
        Assert.Equal(long.MaxValue, accumulated.TotalTokens);
    }
    [Theory]
    [InlineData(BloomProvider.Gemini)] [InlineData(BloomProvider.OpenAI)] [InlineData(BloomProvider.Anthropic)]
    public async Task Extreme_provider_usage_exhausts_budget_and_failed_followup_does_not_reset_accounting(BloomProvider provider)
    {
        var requests = 0;
        var handler = new Handler((_, _) =>
        {
            requests++;
            var json = provider switch
            {
                BloomProvider.Gemini => """{"candidates":[{"finishReason":"STOP","content":{"role":"model","parts":[{"text":"respuesta"}]}}],"usageMetadata":{"promptTokenCount":9223372036854775807,"candidatesTokenCount":9223372036854775807,"thoughtsTokenCount":1}}""",
                BloomProvider.OpenAI => """{"choices":[{"finish_reason":"stop","message":{"content":"respuesta"}}],"usage":{"prompt_tokens":9223372036854775807,"completion_tokens":1}}""",
                _ => """{"stop_reason":"end_turn","content":[{"type":"text","text":"respuesta"}],"usage":{"input_tokens":9223372036854775807,"cache_creation_input_tokens":1,"cache_read_input_tokens":1,"output_tokens":1}}"""
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        });
        var client = new BloomProviderClient(new(provider, BloomConfiguration.DefaultModel(provider)), new HttpClient(handler));
        var tools = new Tools(); var chat = new BloomConversation(client, tools);
        await Assert.ThrowsAsync<BloomException>(() => chat.SendAsync("fake-key", "hola"));
        Assert.Equal(long.MaxValue, chat.Usage.TotalTokens); Assert.Equal(0, tools.Prepared);
        await Assert.ThrowsAsync<BloomException>(() => chat.SendAsync("fake-key", "otra vez"));
        Assert.Equal(long.MaxValue, chat.Usage.TotalTokens); Assert.Equal(1, requests);
    }
    [Theory]
    [InlineData(-1L)] [InlineData(long.MinValue)]
    public async Task Negative_provider_usage_is_rejected_before_tools(long reported)
    {
        var payload = "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"respuesta\"}}],\"usage\":{\"prompt_tokens\":" + reported.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"completion_tokens\":1}}";
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) }));
        var client = new BloomProviderClient(new(BloomProvider.OpenAI, "gpt-5-mini"), new HttpClient(handler));
        var tools = new Tools(); var chat = new BloomConversation(client, tools);
        await Assert.ThrowsAsync<BloomException>(() => chat.SendAsync("fake-key", "hola"));
        Assert.Equal(0, tools.Prepared); Assert.False(chat.HasPending);
    }
    [Theory]
    [InlineData("{\"role\":\"user\",\"parts\":[{\"functionCall\":{\"name\":\"scan_pc\",\"args\":{}}}]}")]
    [InlineData("{\"role\":\"model\",\"parts\":[{\"text\":\"hola\",\"functionCall\":{\"name\":\"scan_pc\",\"args\":{}}}]}")]
    [InlineData("{\"role\":\"model\",\"parts\":[{\"functionCall\":{\"name\":\"scan_pc\",\"args\":[]}}]}")]
    public async Task Malformed_or_ambiguous_canonical_messages_never_reach_network(string message)
    {
        var client = new BloomProviderClient(BloomConfiguration.Default, new HttpClient(new Handler((_, _) => throw new Exception("No debe enviar"))));
        await Assert.ThrowsAsync<BloomException>(() => client.SendAsync("fake-key", [Json(message)]));
    }
    [Fact]
    public async Task Neutral_messages_can_be_sent_without_Gemini_specific_fields()
    {
        var client = new BloomProviderClient(new(BloomProvider.OpenAI, "gpt-5-mini"), new HttpClient(new Handler(async (request, token) =>
        {
            var body = await request.Content!.ReadAsStringAsync(token); Assert.DoesNotContain("parts", body); Assert.DoesNotContain("thoughtSignature", body);
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"Respuesta"}}],"usage":{"prompt_tokens":1,"completion_tokens":1}}""") };
        })));
        var reply = await client.SendAsync("fake-key", new BloomTurn[] { new(BloomRole.User, [new BloomTextPart("consulta")]) });
        Assert.Equal(BloomRole.Assistant, reply.Message.Role); Assert.Equal("Respuesta", Assert.IsType<BloomTextPart>(Assert.Single(reply.Message.Parts)).Text);
    }
    [Fact]
    public async Task Credential_facade_uses_only_the_selected_provider_and_does_not_return_secrets()
    {
        var stores = Enum.GetValues<BloomProvider>().ToDictionary(provider => provider, _ => new Keys());
        var session = new BloomSession(new Tools(), provider => stores[provider], _ => new PlainClient());
        session.LoadConfiguration(); Assert.False(session.HasKey);
        session.SaveKey("fake-gemini"); session.Configure(new(BloomProvider.OpenAI, "gpt-5-mini")); Assert.False(session.HasKey);
        session.SaveKey("fake-openai"); await session.SendAsync("hola"); session.DeleteKey();
        Assert.Null(stores[BloomProvider.OpenAI].Read()); Assert.Equal("fake-gemini", stores[BloomProvider.Gemini].Read());
        Assert.Equal("Cliente de prueba", session.ModelLabel);
    }
    private sealed class Keys : IBloomKeyStore
    {
        private string? key; public string? Read() => key; public void Save(string value) => key = value; public void Delete() => key = null;
    }
    private sealed class PlainClient : IBloomClient
    {
        public string ModelLabel => "Cliente de prueba";
        public Task<BloomReply> SendAsync(string key, IReadOnlyList<JsonElement> contents, CancellationToken token = default) => Task.FromResult(new BloomReply(Json("""{"role":"model","parts":[{"text":"Respuesta local de prueba"}]}""")));
    }
    private sealed class BudgetClient : IBloomClient
    {
        public int Calls;
        public Task<BloomReply> SendAsync(string key, IReadOnlyList<JsonElement> contents, CancellationToken token = default)
        { Calls++; return Task.FromResult(new BloomReply(Json("""{"role":"model","parts":[{"functionCall":{"name":"scan_pc","args":{}}}]}"""), new(60001, 0))); }
    }
    private sealed class Tools : IBloomTools
    {
        public int Prepared;
        public Task<BloomAction> PrepareAsync(BloomCall call, CancellationToken token) { Prepared++; throw new Exception(); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => respond(request, token); }
    private sealed class SlowStream : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { } public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) { await Task.Delay(Timeout.Infinite, token); return 0; }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
