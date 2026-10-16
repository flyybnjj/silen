using Silen.Core;

namespace Silen.Windows;

internal static class DeviceClassifier
{
    internal static (DeviceKind Kind, string Evidence) Classify(int? pcSystemType, IEnumerable<int>? chassisTypes, bool batteryPresent = false)
    {
        var chassis = chassisTypes?.Distinct().ToArray() ?? [];
        var systemKind = pcSystemType switch
        {
            2 => DeviceKind.Laptop,
            1 or 3 or 4 or 5 or 6 or 7 => DeviceKind.Desktop,
            _ => DeviceKind.Unknown
        };
        var portableChassis = chassis.Any(value => value is 8 or 9 or 10 or 11 or 14 or 30 or 31 or 32);
        var desktopChassis = chassis.Any(value => value is 3 or 4 or 5 or 6 or 7 or 13 or 15 or 16 or 17 or 23 or 24 or 35 or 36);
        var chassisKind = portableChassis == desktopChassis ? DeviceKind.Unknown : portableChassis ? DeviceKind.Laptop : DeviceKind.Desktop;
        var evidence = $"PCSystemType: {pcSystemType?.ToString() ?? "no disponible"}; chassis: {(chassis.Length == 0 ? "no disponible" : string.Join(", ", chassis))}.";
        if (portableChassis && desktopChassis || systemKind != DeviceKind.Unknown && chassisKind != DeviceKind.Unknown && systemKind != chassisKind)
            return (DeviceKind.Unknown, evidence + " El firmware entrega tipos incompatibles.");
        var kind = systemKind != DeviceKind.Unknown ? systemKind : chassisKind;
        if (kind == DeviceKind.Unknown) evidence += " No hay evidencia suficiente para identificar portátil o escritorio.";
        if (batteryPresent) evidence += " Una batería puede pertenecer a una UPS; no determina el tipo de equipo.";
        return (kind, evidence);
    }
}
