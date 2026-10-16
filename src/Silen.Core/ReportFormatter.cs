using System.Globalization;
using System.Text;

namespace Silen.Core;

public static class ReportFormatter
{
    public static string ToMarkdown(SystemSnapshot snapshot, IReadOnlyList<Finding> findings)
    {
        snapshot = ReportPrivacy.ForSharing(snapshot);
        var text = new StringBuilder();
        text.AppendLine("# Diagnóstico de Silen").AppendLine();
        text.AppendLine($"Observación: {snapshot.CapturedAt:yyyy-MM-dd HH:mm:ss zzz}.").AppendLine();
        text.AppendLine("Los datos corresponden a esta observación. Las recomendaciones no garantizan una mejora y las guías requieren elegir los cambios en Windows.").AppendLine();
        text.AppendLine("## Equipo").AppendLine();
        text.AppendLine($"- Tipo de equipo: {snapshot.Machine.DeviceKind switch { DeviceKind.Laptop => "Portátil", DeviceKind.Desktop => "Escritorio", _ => "No identificado" }}");
        text.AppendLine($"- Fabricante y modelo: {Cell(snapshot.Machine.Manufacturer)} {Cell(snapshot.Machine.Model)}");
        text.AppendLine($"- Nombre comercial: {Cell(snapshot.Machine.ProductName)}");
        text.AppendLine($"- SKU: {Cell(snapshot.Machine.SystemSku)}");
        text.AppendLine($"- CPU: {Cell(snapshot.Machine.CpuName)}");
        text.AppendLine($"- GPU: {Cell(snapshot.Machine.GpuName)}");
        text.AppendLine($"- Sistema: {Cell(snapshot.Machine.OsName)} {Cell(snapshot.Machine.OsVersion)}");
        text.AppendLine($"- RAM: {Value(snapshot.Machine.TotalMemoryGb > 0 ? snapshot.Machine.TotalMemoryGb : null, "GB")}");
        text.AppendLine($"- Plan energético: {Cell(snapshot.Machine.PowerPlanName)}").AppendLine();
        text.AppendLine("## Mediciones").AppendLine();
        text.AppendLine($"- Muestras: {snapshot.Samples.Count}");
        text.AppendLine($"- CPU media: {Value(RecommendationEngine.Average(snapshot.Samples.Select(x => x.CpuPercent)), "%")}");
        text.AppendLine($"- Memoria media: {Value(RecommendationEngine.Average(snapshot.Samples.Select(x => x.MemoryPercent)), "%")}");
        text.AppendLine($"- GPU media: {Value(RecommendationEngine.Average(snapshot.Samples.Select(x => x.GpuPercent)), "%")}");
        text.AppendLine($"- Temperatura CPU media: {Value(RecommendationEngine.Average(snapshot.Samples.Select(x => x.CpuTemperature)), "°C")}");
        text.AppendLine($"- Temperatura GPU media: {Value(RecommendationEngine.Average(snapshot.Samples.Select(x => x.GpuTemperature)), "°C")}").AppendLine();
        text.AppendLine("## Recomendaciones").AppendLine();
        if (findings.Count == 0) text.AppendLine("No se encontraron señales en las reglas comprobadas. Esto no descarta otras causas de rendimiento.");
        foreach (var finding in findings)
        {
            text.AppendLine($"### {Cell(finding.Title)}").AppendLine();
            text.AppendLine(Cell(finding.Description)).AppendLine();
            text.AppendLine($"Evidencia: {Cell(finding.Evidence)}").AppendLine();
        }
        text.AppendLine("## Almacenamiento").AppendLine();
        text.AppendLine("| Unidad | Tipo | Total | Libre |");
        text.AppendLine("| --- | --- | --- | --- |");
        foreach (var drive in snapshot.Drives)
            text.AppendLine($"| {Cell(drive.Name)} | {Cell(drive.Kind)} | {Value(drive.TotalGb, "GB")} | {Value(drive.FreeGb, "GB")} |");
        text.AppendLine().AppendLine("## Drivers instalados").AppendLine();
        text.AppendLine("La fecha no demuestra por sí sola que haya una actualización compatible.").AppendLine();
        text.AppendLine("| Dispositivo | Proveedor | Versión | Fecha | Firma | Código de problema |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var driver in snapshot.Drivers)
            text.AppendLine($"| {Cell(driver.DeviceName)} | {Cell(driver.Provider)} | {Cell(driver.Version)} | {Cell(driver.Date)} | {(driver.Signed.HasValue ? driver.Signed.Value ? "Sí" : "No" : "No disponible")} | {driver.ProblemCode?.ToString() ?? "No disponible"} |");
        text.AppendLine().AppendLine("## Procesos por memoria").AppendLine();
        foreach (var process in snapshot.Processes)
            text.AppendLine($"- {Cell(process.Name)}: {Value(process.MemoryMb, "MB")} (PID {process.Id})");
        if (snapshot.Notes.Count > 0)
        {
            text.AppendLine().AppendLine("## Datos no disponibles").AppendLine();
            foreach (var note in snapshot.Notes) text.AppendLine($"- {Cell(note)}");
        }
        return text.ToString();
    }

    private static string Value(double? number, string unit) => number.HasValue && double.IsFinite(number.Value)
        ? $"{number.Value.ToString("0.#", CultureInfo.GetCultureInfo("es-CL"))} {unit}" : "No disponible";
    private static string Cell(string? value) => string.IsNullOrWhiteSpace(value) ? "No disponible" :
        value.Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
