using System.Globalization;

namespace Silen.Core;

public static class RecommendationEngine
{
    public static IReadOnlyList<Finding> Analyze(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var findings = new List<Finding>();
        foreach (var (drive, index) in snapshot.Drives.Select((drive, index) => (drive, index)))
        {
            if (double.IsFinite(drive.TotalGb) && double.IsFinite(drive.FreeGb) &&
                drive.TotalGb > 0 && drive.FreeGb >= 0 && drive.FreePercent < 10)
                findings.Add(new($"storage-{index}", $"Poco espacio en {drive.Name}",
                    "Revisa las recomendaciones de almacenamiento. Elige qué archivos o aplicaciones puedes eliminar.",
                    $"{Number(drive.FreeGb)} GB libres de {Number(drive.TotalGb)} GB ({Number(drive.FreePercent!.Value)}%).",
                    Severity.Warning, "Almacenamiento", ActionKind.OpenStorage));
        }

        var memory = Values(snapshot.Samples.Select(s => s.MemoryPercent), percent: true);
        if (memory.Length >= 3 && memory.Average() >= 85)
        {
            var processes = string.Join(", ", snapshot.Processes.OrderByDescending(p => p.MemoryMb).Take(3)
                .Select(p => $"{p.Name}: {Number(p.MemoryMb)} MB"));
            findings.Add(new("memory-pressure", "La memoria estuvo muy ocupada",
                "Revisa las aplicaciones que más memoria usan. Repite la observación con tu carga habitual para confirmar si necesitas cerrar aplicaciones o ampliar RAM.",
                $"Uso medio: {Number(memory.Average())}% en {SpanishText.Count(memory.Length, "muestra", "muestras")}." +
                (processes.Length > 0 ? $" Procesos: {processes}." : ""),
                Severity.Warning, "Memoria", ActionKind.OpenTaskManager));
        }

        var cpu = Values(snapshot.Samples.Select(s => s.CpuPercent), percent: true);
        if (cpu.Length >= 3 && cpu.Average() >= 85)
            findings.Add(new("cpu-load", "Carga alta de CPU durante la observación",
                "Revisa qué tareas estaban activas. Una observación breve no identifica por sí sola el componente que limita un juego.",
                $"Uso medio: {Number(cpu.Average())}% en {SpanishText.Count(cpu.Length, "muestra", "muestras")}.",
                Severity.Warning, "Procesador", ActionKind.OpenTaskManager));

        AddTemperature(findings, snapshot.Samples.Select(s => s.CpuTemperature), "cpu", "CPU");
        AddTemperature(findings, snapshot.Samples.Select(s => s.GpuTemperature), "gpu", "GPU");

        foreach (var (driver, index) in snapshot.Drivers.Select((driver, index) => (driver, index)))
        {
            if (driver.ProblemCode is > 0)
                findings.Add(new($"device-{index}", $"Revisa el dispositivo {driver.DeviceName}",
                    "Windows informa una incidencia en este dispositivo. Consulta las actualizaciones compatibles y el soporte de su fabricante.",
                    $"Código de dispositivo: {driver.ProblemCode}. Versión instalada: {Known(driver.Version)}.",
                    Severity.Warning, "Drivers", ActionKind.OpenOptionalDrivers));
            else if (driver.Signed == false)
                findings.Add(new($"driver-signature-{index}", $"Comprueba el origen de {driver.DeviceName}",
                    "El inventario no informa una firma digital. Comprueba si el fabricante ofrece un paquete firmado compatible; esto no demuestra que el driver sea malicioso.",
                    $"Proveedor: {Known(driver.Provider)}. Versión: {Known(driver.Version)}.",
                    Severity.Suggestion, "Drivers", ActionKind.OpenWindowsUpdate));
        }

        if (string.Equals(snapshot.Machine.PowerPlanGuid, "a1841308-3541-4fab-bc81-f71556f20b4a", StringComparison.OrdinalIgnoreCase))
            findings.Add(new("power-saving", "El plan de energía prioriza el ahorro",
                "Puedes revisar el equilibrio entre rendimiento, temperatura y autonomía. Un modo de mayor rendimiento puede consumir más batería y elevar la temperatura.",
                $"Plan activo: {Known(snapshot.Machine.PowerPlanName)}. " +
                (snapshot.Machine.OnBattery ? "El equipo funciona con batería." : "Revisa el modo adecuado para tu uso."),
                Severity.Suggestion, "Energía", ActionKind.OpenPower));

        if (snapshot.Startup.Count > 0)
            findings.Add(new("startup-review", "Revisa las aplicaciones de inicio",
                "Elige cuáles necesitas al iniciar sesión. Mantén las utilidades que usan tus dispositivos y las aplicaciones que sí necesitas.",
                $"{snapshot.Startup.Count} entradas encontradas. Su presencia no implica un problema de rendimiento.",
                Severity.Info, "Inicio", ActionKind.OpenStartup));

        if (snapshot.Notes.Count > 0)
            findings.Add(new("partial-data", "Algunos datos no estuvieron disponibles",
                "El diagnóstico conserva las mediciones obtenidas. Puedes repetirlo con los permisos adecuados o revisar el soporte del hardware.",
                string.Join(" · ", snapshot.Notes), Severity.Info, "Diagnóstico"));

        return findings.OrderByDescending(f => f.Severity).ToArray();
    }

    private static void AddTemperature(List<Finding> findings, IEnumerable<double?> samples, string id, string name)
    {
        var values = Values(samples, percent: false);
        var hot = values.Count(v => v >= 90);
        if (hot < 3) return;
        findings.Add(new($"{id}-temperature", $"Revisa la temperatura de {name}",
            "Se observaron varias lecturas altas. Revisa ventilación y refrigeración, y compara con el límite del modelo según su fabricante. Este aviso no confirma throttling.",
            $"{hot} lecturas de al menos 90 °C. Máxima observada: {Number(values.Max())} °C.",
            Severity.Warning, "Temperaturas"));
    }

    public static double? Average(IEnumerable<double?> values)
    {
        var finite = values.Where(v => v.HasValue && double.IsFinite(v.Value)).Select(v => v!.Value).ToArray();
        return finite.Length > 0 ? finite.Average() : null;
    }

    private static double[] Values(IEnumerable<double?> values, bool percent) => values
        .Where(v => v.HasValue && double.IsFinite(v.Value) && v.Value >= 0 && (!percent || v.Value <= 100))
        .Select(v => v!.Value).ToArray();
    private static string Number(double number) => number.ToString("0.#", CultureInfo.GetCultureInfo("es-CL"));
    private static string Known(string value) => string.IsNullOrWhiteSpace(value) ? "no disponible" : value;
}
