namespace Silen.Core;

public sealed record SnapshotDifference(double? CpuPercentagePointChange, double? MemoryPercentagePointChange, double? CpuTemperatureChange, double? GpuTemperatureChange);

public static class SnapshotComparison
{
    public static SnapshotDifference Compare(SystemSnapshot before, SystemSnapshot after) => new(
        Difference(before.Samples.Select(s => s.CpuPercent), after.Samples.Select(s => s.CpuPercent)),
        Difference(before.Samples.Select(s => s.MemoryPercent), after.Samples.Select(s => s.MemoryPercent)),
        Difference(before.Samples.Select(s => s.CpuTemperature), after.Samples.Select(s => s.CpuTemperature)),
        Difference(before.Samples.Select(s => s.GpuTemperature), after.Samples.Select(s => s.GpuTemperature)));

    private static double? Difference(IEnumerable<double?> before, IEnumerable<double?> after)
    {
        var a = RecommendationEngine.Average(before);
        var b = RecommendationEngine.Average(after);
        return a.HasValue && b.HasValue ? b.Value - a.Value : null;
    }
}
