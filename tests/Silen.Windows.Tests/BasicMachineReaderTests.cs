using Silen.Core;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class BasicMachineReaderTests
{
    [Fact]
    public void Preview_reads_only_model_cpu_and_capacity_without_serials_or_usage_values()
    {
        var requested = new List<string>();
        var result = BasicMachineReader.Read((_, name) =>
        {
            requested.Add(name);
            return name switch { "SystemManufacturer" => " Marca ", "SystemProductName" => "Modelo", "ProcessorNameString" => "CPU", _ => throw new InvalidOperationException() };
        }, () => 16UL * 1073741824);
        Assert.Equal(new[] { "SystemManufacturer", "SystemProductName", "ProcessorNameString" }, requested);
        Assert.Equal("Marca", result.Manufacturer);
        Assert.Equal("Modelo", result.Model);
        Assert.Equal("CPU", result.CpuName);
        Assert.Equal(16, result.TotalMemoryGb);
        Assert.Equal(DeviceKind.Unknown, result.DeviceKind);
        Assert.Equal("", result.GpuName);
    }

    [Fact]
    public void Denied_registry_and_unknown_memory_do_not_prevent_the_full_scan()
    {
        var result = BasicMachineReader.Read((_, _) => throw new UnauthorizedAccessException(), () => null);
        Assert.Equal("", result.Manufacturer);
        Assert.Equal("", result.Model);
        Assert.Equal("", result.CpuName);
        Assert.Equal(0, result.TotalMemoryGb);
        Assert.True(result.LogicalCores > 0);
    }
}
