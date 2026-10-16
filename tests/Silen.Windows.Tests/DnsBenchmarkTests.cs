using System.Net;
using Silen.Windows;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class DnsBenchmarkTests
{
    [Fact]
    public async Task Benchmark_reports_real_probe_progress_and_ranks_reliable_medians_before_partial_failures()
    {
        var updates = new List<DnsBenchmarkRow>();
        var service = new DnsBenchmarkService(new FakeProbe());
        var rows = await service.MeasureAsync([new("Lento", "1.1.1.1"), new("Rápido", "8.8.8.8"), new("Parcial", "9.9.9.9")], new InlineProgress(updates.Add));
        Assert.Equal(9, updates.Count);
        Assert.Equal("Rápido", rows[0].Name); Assert.Equal(20, rows[0].MedianMs);
        Assert.Equal("Lento", rows[1].Name); Assert.Equal(1, rows[2].Failed);
    }
    [Fact]
    public void Responses_with_wrong_transaction_or_invalid_question_are_not_successful_samples()
    {
        var query = DnsWireProtocol.CreateQuery(42, "www.example.com");
        var answer = query.Concat(new byte[] { 0xc0, 12, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 1, 2, 3, 4 }).ToArray();
        answer[2] = 0x81; answer[3] = 0x80; answer[7] = 1;
        Assert.True(DnsWireProtocol.HasAnswer(query, answer));
        answer[1] = 43; Assert.False(DnsWireProtocol.HasAnswer(query, answer));
        answer[1] = 42; answer[13] ^= 1; Assert.False(DnsWireProtocol.HasAnswer(query, answer));
    }
    [Fact]
    public void A_compression_pointer_loop_is_not_a_valid_answer()
    {
        var query = DnsWireProtocol.CreateQuery(42, "www.example.com");
        var answer = query.Concat(new byte[] { 0xc0, (byte)query.Length, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 1, 2, 3, 4 }).ToArray();
        answer[2] = 0x81; answer[3] = 0x80; answer[7] = 1;
        Assert.False(DnsWireProtocol.HasAnswer(query, answer));
    }
    [Fact]
    public async Task Cancelled_benchmark_does_not_keep_testing_or_invent_a_winner()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DnsBenchmarkService(new FakeProbe()).MeasureAsync([new("Prueba", "1.1.1.1")], cancellationToken: cancellation.Token));
    }
    private sealed class FakeProbe : IDnsProbe
    {
        public Task<double?> QueryAsync(IPAddress server, string domain, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            double? value = server.ToString() switch { "1.1.1.1" => 100, "8.8.8.8" => domain == "www.microsoft.com" ? 100 : 20, _ => domain == "www.microsoft.com" ? null : 1 };
            return Task.FromResult(value);
        }
    }
    private sealed class InlineProgress(Action<DnsBenchmarkRow> update) : IProgress<DnsBenchmarkRow>
    { public void Report(DnsBenchmarkRow value) => update(value); }
}
