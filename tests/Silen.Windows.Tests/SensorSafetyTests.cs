using Xunit;

namespace Silen.Windows.Tests;

public sealed class SensorSafetyTests
{
    [Fact]
    public void Read_only_scan_disables_the_providers_that_use_a_kernel_sensor_driver()
    {
        var computer = HardwareSensors.CreateReadOnlyComputer();
        Assert.False(computer.IsCpuEnabled);
        Assert.False(computer.IsMotherboardEnabled);
        Assert.False(computer.IsControllerEnabled);
        Assert.False(computer.IsStorageEnabled);
        Assert.False(computer.IsPowerMonitorEnabled);
        Assert.True(computer.IsGpuEnabled);
    }
}
