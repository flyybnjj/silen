namespace Silen.Windows;

internal sealed record ServiceSnapshot(int StartType, bool Running);

internal static class OptionalServices
{
    internal sealed record Group(string Id, string Name, string Title, string Description);
    internal static readonly Group[] Groups =
    [
        new("service-diagtrack", "DiagTrack", "Desactivar experiencias de usuario y diagnósticos", "Detiene y deshabilita DiagTrack. Reduce el envío de diagnósticos gestionado por este servicio; afecta experiencias conectadas. No desactiva toda la telemetría ni garantiza más FPS."),
        new("service-maps", "MapsBroker", "Desactivar mapas descargados", "Detiene y deshabilita MapsBroker. Las apps que usan mapas sin conexión pueden dejar de acceder a ellos. Si el servicio ya está detenido, no libera CPU en uso."),
        new("service-fax", "Fax", "Desactivar el servicio de fax", "Detiene y deshabilita Fax cuando está instalado. Se pierde la capacidad de enviar y recibir fax hasta restaurarlo; no cambia la impresión normal.")
    ];
    internal static Group Find(string id) => Groups.FirstOrDefault(group => group.Id == id)
        ?? throw new ArgumentException("Servicio fuera del catálogo permitido.");
    internal static void ValidateName(string name)
    {
        if (!Groups.Any(group => group.Name == name)) throw new ArgumentException("Servicio fuera del catálogo permitido.");
    }
    internal static bool Allows(string tool, OptimizationValueChange value) => Groups.Any(group => group.Id == tool && group.Name == value.Key) &&
        value.Name == "" && value.Previous.Exists && value.Previous.Kind == Microsoft.Win32.RegistryValueKind.DWord &&
        (value.Kind == "ServiceStart" && value.Previous.Number is >= 2 and <= 4 && value.Intended.Same(RegistryValueState.Dword(4)) ||
         value.Kind == "ServiceRunning" && value.Previous.Number is 0 or 1 && value.Intended.Same(RegistryValueState.Dword(0)));
}
