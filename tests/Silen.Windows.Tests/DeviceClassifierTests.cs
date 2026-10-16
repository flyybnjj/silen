using Silen.Core;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class DeviceClassifierTests
{
    [Theory]
    [InlineData(2, new[] { 10 }, DeviceKind.Laptop)]
    [InlineData(1, new[] { 3 }, DeviceKind.Desktop)]
    [InlineData(0, new[] { 9 }, DeviceKind.Laptop)]
    [InlineData(0, new[] { 31 }, DeviceKind.Laptop)]
    [InlineData(0, new[] { 13 }, DeviceKind.Desktop)]
    [InlineData(3, new[] { 0 }, DeviceKind.Desktop)]
    [InlineData(0, new[] { 0 }, DeviceKind.Unknown)]
    public void System_and_chassis_evidence_classifies_the_device(int pcSystemType, int[] chassis, DeviceKind expected)
    {
        var result = DeviceClassifier.Classify(pcSystemType, chassis);
        Assert.Equal(expected, result.Kind);
        Assert.NotEmpty(result.Evidence);
    }

    [Fact]
    public void A_desktop_with_a_ups_is_still_a_desktop() =>
        Assert.Equal(DeviceKind.Desktop, DeviceClassifier.Classify(1, [3], batteryPresent: true).Kind);

    [Fact]
    public void A_battery_without_system_or_chassis_evidence_does_not_prove_a_laptop() =>
        Assert.Equal(DeviceKind.Unknown, DeviceClassifier.Classify(null, null, batteryPresent: true).Kind);

    [Fact]
    public void Conflicting_firmware_evidence_is_reported_as_unknown() =>
        Assert.Equal(DeviceKind.Unknown, DeviceClassifier.Classify(1, [10]).Kind);
}
