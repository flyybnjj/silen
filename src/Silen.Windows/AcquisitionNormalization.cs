using System.Globalization;
using System.Text.RegularExpressions;
using Silen.Core;

namespace Silen.Windows;

internal sealed record DeviceInventory(string Id, string Name, string Manufacturer, string Class, string HardwareId, int? ProblemCode);

internal static class AcquisitionNormalization
{
    internal static double? Percent(double? value) => value is >= 0 and <= 100 && double.IsFinite(value.Value) ? value : null;

    internal static double? CpuTemperature(string? identifier, string? sensorType, double? value) =>
        string.Equals(sensorType, "Temperature", StringComparison.OrdinalIgnoreCase) &&
        (identifier?.StartsWith("/intelcpu/", StringComparison.OrdinalIgnoreCase) == true || identifier?.StartsWith("/amdcpu/", StringComparison.OrdinalIgnoreCase) == true) &&
        value is > -50 and < 150 && double.IsFinite(value.Value) ? value : null;

    internal static double? CpuPercent(ulong idleBefore, ulong kernelBefore, ulong userBefore, ulong idleNow, ulong kernelNow, ulong userNow)
    {
        if (idleNow < idleBefore || kernelNow < kernelBefore || userNow < userBefore) return null;
        double idle = idleNow - idleBefore;
        double total = (kernelNow - kernelBefore) + (double)(userNow - userBefore);
        return total > 0 ? Percent((total - idle) / total * 100) : null;
    }

    internal static string DriverDate(string? value) => value?.Length >= 8 &&
        DateTime.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";

    internal static string PrivateCommand(string value)
    {
        var currentProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (currentProfile.Length > 0) value = value.Replace(currentProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        var redacted = Regex.Replace(value, @"[A-Za-z]:\\Users\\[^\\\r\n""/]+", "%USERPROFILE%", RegexOptions.IgnoreCase);
        return Regex.Replace(redacted, @"\\\\[^\\\r\n""]+", "%NETWORK%");
    }

    internal static List<DriverInfo> MergeDrivers(IReadOnlyDictionary<string, DriverInfo> signed, IEnumerable<DeviceInventory> devices)
    {
        var merged = new List<DriverInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
        {
            if (!seen.Add(device.Id)) continue;
            var info = signed.GetValueOrDefault(device.Id) ?? new DriverInfo();
            merged.Add(info with
            {
                DeviceName = string.IsNullOrWhiteSpace(info.DeviceName) ? device.Name : info.DeviceName,
                Manufacturer = string.IsNullOrWhiteSpace(info.Manufacturer) ? device.Manufacturer : info.Manufacturer,
                DeviceClass = string.IsNullOrWhiteSpace(info.DeviceClass) ? device.Class : info.DeviceClass,
                HardwareId = device.HardwareId,
                ProblemCode = device.ProblemCode
            });
        }
        foreach (var (id, info) in signed)
            if (seen.Add(id)) merged.Add(info);
        return merged;
    }
}
