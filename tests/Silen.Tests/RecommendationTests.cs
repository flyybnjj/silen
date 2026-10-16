using Silen.Core;
using Xunit;

namespace Silen.Tests;

public class RecommendationTests
{
    [Fact]
    public void LowDiskSpaceOffersStorageGuideWithMeasuredEvidence()
    {
        var snapshot = new SystemSnapshot { Drives = [new() { Name = "C:\\", TotalGb = 100, FreeGb = 9 }] };
        var finding = Assert.Single(RecommendationEngine.Analyze(snapshot));
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal(ActionKind.OpenStorage, finding.Action);
        Assert.Contains("9", finding.Evidence);
    }

    [Fact]
    public void TenPercentFreeDoesNotTriggerLowSpaceWarning()
    {
        var snapshot = new SystemSnapshot { Drives = [new() { TotalGb = 100, FreeGb = 10 }] };
        Assert.Empty(RecommendationEngine.Analyze(snapshot));
    }

    [Fact]
    public void UnknownDiskCapacityDoesNotPretendDiskIsFull()
    {
        var snapshot = new SystemSnapshot { Drives = [new() { TotalGb = 0, FreeGb = 0 }] };
        Assert.Empty(RecommendationEngine.Analyze(snapshot));
    }

    [Fact]
    public void SustainedMemoryPressureNamesTheLargestProcesses()
    {
        var snapshot = new SystemSnapshot
        {
            Samples = [new() { MemoryPercent = 85 }, new() { MemoryPercent = 86 }, new() { MemoryPercent = 87 }],
            Processes = [new("Game", 42, 4096)]
        };
        var finding = Assert.Single(RecommendationEngine.Analyze(snapshot));
        Assert.Equal("memory-pressure", finding.Id);
        Assert.Contains("Game", finding.Evidence);
        Assert.Equal(ActionKind.OpenTaskManager, finding.Action);
    }

    [Fact]
    public void OneBusySampleDoesNotDiagnoseSustainedPressure()
    {
        var snapshot = new SystemSnapshot { Samples = [new() { MemoryPercent = 99, CpuPercent = 99 }] };
        Assert.Empty(RecommendationEngine.Analyze(snapshot));
    }

    [Fact]
    public void HighCpuIsReportedAsObservedLoadWithTaskManagerGuide()
    {
        var snapshot = new SystemSnapshot { Samples = Enumerable.Range(0, 3).Select(_ => new MetricSample { CpuPercent = 92 }).ToList() };
        var finding = Assert.Single(RecommendationEngine.Analyze(snapshot));
        Assert.Equal("cpu-load", finding.Id);
        Assert.Equal(ActionKind.OpenTaskManager, finding.Action);
    }

    [Fact]
    public void MissingAndInvalidSensorsDoNotProduceThermalWarnings()
    {
        var snapshot = new SystemSnapshot { Samples = Enumerable.Range(0, 4).Select(_ => new MetricSample { CpuTemperature = double.NaN, GpuTemperature = null }).ToList() };
        Assert.Empty(RecommendationEngine.Analyze(snapshot));
    }

    [Fact]
    public void RepeatedHeatPromptsCheckingCoolingWithoutClaimingThrottling()
    {
        var snapshot = new SystemSnapshot { Samples = [new() { CpuTemperature = 90 }, new() { CpuTemperature = 94 }, new() { CpuTemperature = 93 }] };
        var finding = Assert.Single(RecommendationEngine.Analyze(snapshot));
        Assert.Equal("cpu-temperature", finding.Id);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal(ActionKind.None, finding.Action);
    }

    [Fact]
    public void OldSignedDriverAloneIsNotDeclaredOutdated()
    {
        var snapshot = new SystemSnapshot { Drivers = [new() { DeviceName = "System", Date = "2006-06-21", Version = "10.0", Signed = true, ProblemCode = 0 }] };
        Assert.Empty(RecommendationEngine.Analyze(snapshot));
    }

    [Fact]
    public void DeviceErrorOffersOfficialDriverGuide()
    {
        var snapshot = new SystemSnapshot { Drivers = [new() { DeviceName = "Audio", ProblemCode = 28 }] };
        var finding = Assert.Single(RecommendationEngine.Analyze(snapshot));
        Assert.Equal(ActionKind.OpenOptionalDrivers, finding.Action);
        Assert.Contains("28", finding.Evidence);
    }

    [Fact]
    public void UnsignedDriverSuggestsCheckingOrigin()
    {
        var snapshot = new SystemSnapshot { Drivers = [new() { DeviceName = "Custom device", Signed = false }] };
        var finding = Assert.Single(RecommendationEngine.Analyze(snapshot));
        Assert.Equal(Severity.Suggestion, finding.Severity);
        Assert.Equal(ActionKind.OpenWindowsUpdate, finding.Action);
    }

    [Fact]
    public void StartupIsAnInformationalReviewAndNotAnAutomaticRemoval()
    {
        var snapshot = new SystemSnapshot { Startup = [new("OneDrive", "known executable", "Run")] };
        var finding = Assert.Single(RecommendationEngine.Analyze(snapshot));
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal(ActionKind.OpenStartup, finding.Action);
    }

    [Fact]
    public void PowerSavingPlanOffersUserChoice()
    {
        var snapshot = new SystemSnapshot { Machine = new() { PowerPlanGuid = "a1841308-3541-4fab-bc81-f71556f20b4a", IsLaptop = true, OnBattery = true } };
        var finding = Assert.Single(RecommendationEngine.Analyze(snapshot));
        Assert.Equal(ActionKind.OpenPower, finding.Action);
        Assert.Contains("batería", finding.Description);
    }

    [Fact]
    public void PartialAcquisitionPreservesTheOtherFindings()
    {
        var snapshot = new SystemSnapshot { Notes = ["Sensores no disponibles"], Drives = [new() { TotalGb = 100, FreeGb = 2 }] };
        var findings = RecommendationEngine.Analyze(snapshot);
        Assert.Equal(2, findings.Count);
        Assert.Equal(Severity.Warning, findings[0].Severity);
        Assert.Contains(findings, x => x.Id == "partial-data");
    }
}
