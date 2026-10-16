using Silen.Core;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class AcquisitionNormalizationTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(double.NaN, null)]
    [InlineData(double.PositiveInfinity, null)]
    [InlineData(-1d, null)]
    [InlineData(101d, null)]
    [InlineData(0d, 0d)]
    [InlineData(100d, 100d)]
    public void Missing_or_invalid_load_is_unknown(double? input, double? expected) =>
        Assert.Equal(expected, AcquisitionNormalization.Percent(input));

    [Fact]
    public void Cpu_delta_removes_idle_time_from_kernel_time() =>
        Assert.Equal(75d, AcquisitionNormalization.CpuPercent(100, 200, 100, 200, 400, 300));

    [Fact]
    public void Invalid_cpu_counter_delta_is_unknown() =>
        Assert.Null(AcquisitionNormalization.CpuPercent(100, 200, 100, 50, 400, 300));

    [Theory]
    [InlineData("20241002123456.000000+000", "2024-10-02")]
    [InlineData(null, "")]
    [InlineData("broken", "")]
    public void Driver_dates_are_normalized_without_inventing_values(string? input, string expected) =>
        Assert.Equal(expected, AcquisitionNormalization.DriverDate(input));

    [Theory]
    [InlineData("/intelcpu/0/temperature/0", "Temperature", 65d, 65d)]
    [InlineData("/amdcpu/0/temperature/1", "Temperature", 91d, 91d)]
    [InlineData("/intelcpu/0/temperature/0", "Temperature", null, null)]
    [InlineData("/intelcpu/0/temperature/0", "Load", 65d, null)]
    [InlineData("/acpi/thermalzone/0", "Temperature", 65d, null)]
    [InlineData("/gpu-nvidia/0/temperature/0", "Temperature", 65d, null)]
    [InlineData("/intelcpu-unknown/0", "Temperature", 65d, null)]
    [InlineData("/amdcpu/0/temperature/0", "Temperature", double.NaN, null)]
    [InlineData("/amdcpu/0/temperature/0", "Temperature", 54000d, null)]
    public void Cpu_temperature_requires_a_real_cpu_provider_identifier(string identifier, string type, double? value, double? expected) =>
        Assert.Equal(expected, AcquisitionNormalization.CpuTemperature(identifier, type, value));

    [Fact]
    public void Commands_do_not_export_account_or_network_machine_names()
    {
        Assert.Equal("\"%USERPROFILE%\\AppData\\Local\\app.exe\" /log %USERPROFILE%\\file.log", AcquisitionNormalization.PrivateCommand("\"C:\\Users\\alice\\AppData\\Local\\app.exe\" /log D:\\Users\\bob\\file.log"));
        Assert.Equal("%NETWORK%\\share\\app.exe", AcquisitionNormalization.PrivateCommand("\\\\workstation\\share\\app.exe"));
    }

    [Fact]
    public void Account_and_network_names_with_spaces_are_fully_redacted()
    {
        Assert.Equal("\"%USERPROFILE%\\app.exe\"", AcquisitionNormalization.PrivateCommand("\"C:\\Users\\Alice Smith\\app.exe\""));
        Assert.Equal("\"%NETWORK%\\share\\app.exe\"", AcquisitionNormalization.PrivateCommand("\"\\\\office workstation\\share\\app.exe\""));
    }

    [Fact]
    public void Driver_merge_preserves_unsigned_and_missing_driver_devices()
    {
        var signed = new Dictionary<string, DriverInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["PCI\\ONE\\instance"] = new() { DeviceName = "GPU", Version = "1.2", Signed = false }
        };
        var devices = new[]
        {
            new DeviceInventory("pci\\one\\INSTANCE", "GPU", "Vendor", "Display", "PCI\\VEN_1234", 0),
            new DeviceInventory("USB\\TWO\\private-serial", "Unknown device", "", "", "USB\\VID_1234", 28)
        };
        var drivers = AcquisitionNormalization.MergeDrivers(signed, devices);
        Assert.Equal(2, drivers.Count);
        Assert.False(drivers[0].Signed);
        Assert.Equal("1.2", drivers[0].Version);
        Assert.Equal(28, drivers[1].ProblemCode);
        Assert.Null(drivers[1].Signed);
        Assert.Empty(drivers[1].Version);
        Assert.Equal("USB\\VID_1234", drivers[1].HardwareId);
        Assert.DoesNotContain("private-serial", drivers[1].HardwareId);
    }
}
