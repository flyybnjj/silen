using Silen.Core;
using System.Globalization;
using Microsoft.Win32;
using System.ComponentModel;

namespace Silen.Windows;

internal static class WindowsUpdateDiagnostics
{
    internal const string UxKey = @"HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
    internal const string PolicyKey = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    internal static readonly string[] PauseValues = ["PauseUpdatesStartTime", "PauseUpdatesExpiryTime", "PauseFeatureUpdatesStartTime", "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime"];
    internal static WindowsUpdateHealth Read(IOptimizationPlatform platform, DateTimeOffset now)
    {
        try
        {
            var service = platform.ReadUpdateService();
            if (service is null) return WindowsUpdateHealth.Unknown("No se encontró el servicio de Windows Update. No se puede confirmar su estado.");
            DateTimeOffset? until = null;
            foreach (var pair in new[] { ("PauseUpdatesStartTime", "PauseUpdatesExpiryTime"), ("PauseFeatureUpdatesStartTime", "PauseFeatureUpdatesEndTime"), ("PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime") })
            {
                var start = ReadDate(platform.ReadValue(UxKey, pair.Item1));
                var end = ReadDate(platform.ReadValue(UxKey, pair.Item2));
                if (start > now) continue;
                if (end > now && (until is null || end > until)) until = end;
                if (start is not null && end is null) throw new InvalidDataException("La pausa local tiene una fecha de inicio sin fecha de término.");
            }
            var managed = IsOne(platform.ReadValue(PolicyKey + @"\AU", "NoAutoUpdate")) ||
                IsOne(platform.ReadValue(PolicyKey, "DisableWindowsUpdateAccess")) ||
                IsOne(platform.ReadValue(PolicyKey, "SetDisableUXWUAccess"));
            var policyPaused = false;
            foreach (var key in new[] { PolicyKey, @"HKLM\SOFTWARE\Microsoft\PolicyManager\current\device\Update" })
            foreach (var kind in new[] { "Feature", "Quality" })
            {
                policyPaused |= IsOne(platform.ReadValue(key, "Pause" + kind + "Updates"));
                var policyStart = platform.ReadValue(key, "Pause" + kind + "UpdatesStartTime");
                if (policyStart.Exists)
                {
                    if (policyStart.Kind == RegistryValueKind.DWord) policyPaused |= policyStart.Number != 0;
                    else if (ReadDate(policyStart) is { } date && date <= now && date.AddDays(35) > now)
                    {
                        policyPaused = true;
                        if (until is null || date.AddDays(35) > until) until = date.AddDays(35);
                    }
                }
            }
            managed |= policyPaused;
            var messages = new List<string>();
            var effectivePause = false;
            foreach (var name in new[] { "PausedFeatureStatus", "PausedQualityStatus" })
            {
                var flag = platform.ReadValue(@"HKLM\SOFTWARE\Microsoft\WindowsUpdate\UpdatePolicy\Settings", name);
                if (flag.Exists && (flag.Kind != RegistryValueKind.DWord || flag.Number is < 0 or > 2))
                    throw new InvalidDataException("Windows devolvió un estado de pausa incompatible.");
                effectivePause |= IsOne(flag);
            }
            if (until is { } pausedUntil) messages.Add($"Windows Update está pausado hasta {pausedUntil.ToLocalTime().ToString("dd-MM-yyyy HH:mm", CultureInfo.GetCultureInfo("es-CL"))}.");
            else if (effectivePause) messages.Add("Windows sigue informando una pausa activa sin fecha de término disponible. Revisa su estado en Windows Update.");
            if (service.StartType == 4) messages.Add("El servicio de Windows Update está deshabilitado.");
            if (!service.Running) messages.Add(service.StartType == 3 ? "El servicio de actualización está detenido; su inicio es manual por demanda." : "El servicio de actualización está detenido.");
            if (policyPaused) messages.Add("Una política mantiene pausadas las actualizaciones. Silen no la sobrescribe; revisa Windows Update o consulta al administrador.");
            else if (managed) messages.Add("Una política configura las actualizaciones automáticas o su interfaz. Silen la conserva; la búsqueda manual comprobará el acceso disponible.");
            if (messages.Count == 0) messages.Add("No se detectó una pausa local y el servicio está en ejecución. La búsqueda comprobará si Windows Update puede ofrecer drivers.");
            return new(true, until is not null || effectivePause || policyPaused, until, service.StartType != 4, service.Running, managed, string.Join(" ", messages), policyPaused);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException or Win32Exception or ArgumentException or InvalidOperationException)
        { return WindowsUpdateHealth.Unknown("No se pudo comprobar Windows Update: " + ex.Message); }
    }
    private static bool IsOne(RegistryValueState state) => state.Exists && state.Kind == RegistryValueKind.DWord && state.Number == 1;
    private static DateTimeOffset? ReadDate(RegistryValueState state)
    {
        if (!state.Exists || state.Kind is RegistryValueKind.String or RegistryValueKind.ExpandString && string.IsNullOrWhiteSpace(state.Text)) return null;
        if (state.Kind is RegistryValueKind.String or RegistryValueKind.ExpandString && DateTimeOffset.TryParse(state.Text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)) return date;
        throw new InvalidDataException("Windows devolvió una fecha de pausa que no se pudo interpretar.");
    }
    internal static bool Allows(string tool, OptimizationValueChange value) => tool == "windows-update-resume" &&
        (value.Kind == "Registry" && value.Key == UxKey && PauseValues.Contains(value.Name) && !value.Intended.Exists ||
         value.Key == "wuauserv" && value.Name == "" && value.Previous.Exists && value.Previous.Kind == RegistryValueKind.DWord &&
         (value.Kind == "ServiceStart" && value.Previous.Number is >= 2 and <= 4 && value.Intended.Same(RegistryValueState.Dword(3)) ||
          value.Kind == "ServiceRunning" && value.Previous.Number is 0 or 1 && value.Intended.Same(RegistryValueState.Dword(1))));
}
