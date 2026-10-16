using Silen.Core;
using Xunit;

namespace Silen.Tests;

public sealed class RamAssessmentTests
{
    [Fact]
    public void Sustained_low_memory_and_commit_pressure_produce_evidence_based_guidance()
    {
        var state = new MemoryObservation(8L << 30, 300L << 20, 15L << 30, 16L << 30, false);
        var advice = RamAssessment.Evaluate(Enumerable.Repeat(state, 5).ToArray());
        Assert.True(advice.Pressure); Assert.Contains("paginación", advice.Message); Assert.Contains("segundo plano", advice.Message);
    }
    [Fact]
    public void A_single_low_sample_or_missing_data_does_not_claim_a_confirmed_memory_problem()
    {
        Assert.False(RamAssessment.Evaluate([new(8L << 30, 300L << 20, 5L << 30, 16L << 30, true)]).Pressure);
        Assert.False(RamAssessment.Evaluate([]).Pressure);
    }

    [Fact]
    public void A_long_gap_or_failed_read_restarts_the_observation_window()
    {
        var now = DateTimeOffset.UtcNow;
        var low = new MemoryObservation(8L << 30, 300L << 20, 15L << 30, 16L << 30, true, now);
        var stale = Enumerable.Range(0, 4).Select(index => low with { At = now.AddMinutes(-10).AddSeconds(index * 2) });
        Assert.False(RamAssessment.Evaluate(stale.Append(low).ToArray()).Pressure);
        Assert.False(RamAssessment.Evaluate(Enumerable.Repeat(low, 5).Append(low with { TotalBytes = 0 }).ToArray()).Pressure);
    }
}
