namespace Silen.Core;

public sealed record MemoryObservation(long TotalBytes, long AvailableBytes, long CommitBytes, long CommitLimitBytes, bool? AutomaticPagefile, DateTimeOffset? At = null)
{
    public double UsedPercent => TotalBytes > 0 ? 100d * (TotalBytes - AvailableBytes) / TotalBytes : 0;
}
public sealed record RamAssessment(bool Pressure, string Message)
{
    public static RamAssessment Evaluate(IReadOnlyList<MemoryObservation> samples)
    {
        var recent = new List<MemoryObservation>();
        foreach (var sample in samples)
        {
            if (sample.TotalBytes <= 0 || sample.AvailableBytes < 0 || sample.AvailableBytes > sample.TotalBytes)
            {
                recent.Clear();
                continue;
            }
            if (recent.Count > 0 && recent[^1].At is { } previous && sample.At is { } at &&
                (at < previous || at - previous > TimeSpan.FromSeconds(5))) recent.Clear();
            recent.Add(sample);
            if (recent.Count > 5) recent.RemoveAt(0);
        }
        var valid = recent.ToArray();
        if (valid.Length == 0) return new(false, "No se pudo medir la memoria. Actualiza el estado para volver a comprobarla.");
        var latest = valid[^1];
        var pressure = valid.Length == 5 && valid.All(sample => sample.AvailableBytes < sample.TotalBytes * .1);
        var commitPressure = valid.Length == 5 && valid.All(sample => sample.CommitLimitBytes > 0 && sample.CommitBytes > sample.CommitLimitBytes * .9);
        var message = pressure ? "La RAM disponible se mantuvo por debajo del 10 % en cinco muestras. Revisa las apps que no necesitas y los ajustes de segundo plano en Optimizar PC. Conserva los que usas para sincronizar o recibir notificaciones."
            : valid.Length < 5 ? "Reuniendo cinco muestras para distinguir una subida puntual de una falta sostenida de memoria."
            : "No se observó falta sostenida de RAM en estas cinco muestras. La memoria en caché puede reutilizarse; no hace falta vaciarla.";
        if (commitPressure) message += " El compromiso de memoria supera el 90 % de su límite: revisa la paginación y las apps abiertas; no desactives el archivo de paginación.";
        if (latest.AutomaticPagefile == false) message += " La paginación tiene configuración manual. Su tamaño depende de la carga y los volcados; no se aplica una fórmula fija ni se cambia automáticamente.";
        return new(pressure || commitPressure, message);
    }
}
