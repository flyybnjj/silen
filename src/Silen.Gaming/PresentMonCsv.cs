using Silen.Core;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.Text;

namespace Silen.Gaming;

public static class PresentMonCsv
{
    private const long MaximumFileBytes = 64L * 1024 * 1024;
    private const int MaximumFrames = 1_000_000;


    public static GamingSession Parse(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumFileBytes)
            throw new InvalidDataException("El CSV supera el límite de 64 MiB; exporta una captura más corta.");

        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        string? headerLine;
        do { cancellationToken.ThrowIfCancellationRequested(); headerLine = reader.ReadLine(); } while (headerLine is not null && string.IsNullOrWhiteSpace(headerLine));
        if (headerLine is null)
            throw new InvalidDataException("El CSV está vacío.");

        var delimiter = CountUnquoted(headerLine, ';') > CountUnquoted(headerLine, ',') ? ";" : ",";
        using var headerParser = CreateParser(new StringReader(headerLine), delimiter);
        string[] header;
        try { header = headerParser.ReadFields() ?? []; }
        catch (MalformedLineException error) { throw new InvalidDataException("La cabecera CSV no es válida.", error); }

        var timingIndex = FindColumn(header, "FrameTime");
        if (timingIndex < 0) timingIndex = FindColumn(header, "MsBetweenPresents");
        if (timingIndex < 0)
            throw new InvalidDataException("No se encontró FrameTime ni MsBetweenPresents. Exporta un CSV de frames de PresentMon; el frame time no es latencia de entrada.");
        var applicationIndex = FindColumn(header, "Application");
        var processIndex = FindColumn(header, "ProcessID");
        var swapChainIndex = FindColumn(header, "SwapChainAddress");

        var times = new List<double>();
        var ignoredRows = 0;
        string? processIdentity = null;
        string? swapChainIdentity = null;
        var processName = "Proceso no identificado";
        using var parser = CreateParser(reader, delimiter);
        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[]? fields;
            try { fields = parser.ReadFields(); }
            catch (MalformedLineException) { ignoredRows++; continue; }
            if (fields is null) continue;
            if (fields.Length != header.Length || !TryParseTime(fields[timingIndex], out var time))
            {
                ignoredRows++;
                continue;
            }

            var processId = 0;
            if (processIndex >= 0 && (!int.TryParse(fields[processIndex], NumberStyles.None, CultureInfo.InvariantCulture, out processId) || processId <= 0))
            {
                ignoredRows++;
                continue;
            }
            var name = applicationIndex >= 0 ? fields[applicationIndex] : "";
            var identity = processIndex >= 0 ? processId.ToString(CultureInfo.InvariantCulture) + "\0" + name : name;
            if (processIdentity is not null && !string.Equals(processIdentity, identity, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El CSV contiene varios procesos. Exporta o filtra un único PID para no combinar sus FPS.");
            processIdentity = identity;
            if (times.Count == 0 && name.Length > 0) processName = name;
            else if (times.Count == 0 && processId > 0) processName = "Proceso " + processId.ToString(CultureInfo.InvariantCulture);

            if (swapChainIndex >= 0)
            {
                var chain = fields[swapChainIndex];
                if (chain.Length == 0) { ignoredRows++; continue; }
                if (swapChainIdentity is not null && !string.Equals(swapChainIdentity, chain, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("El CSV contiene varias swap chains. Filtra la cadena principal del juego para no combinar sus FPS.");
                swapChainIdentity = chain;
            }
            if (times.Count >= MaximumFrames)
                throw new InvalidDataException("El CSV supera el límite de 1.000.000 de frames; exporta una captura más corta.");
            times.Add(time);
        }
        if (times.Count == 0)
            throw new InvalidDataException("El CSV no contiene frame times positivos y finitos válidos.");

        cancellationToken.ThrowIfCancellationRequested();
        var sorted = times.ToArray();
        Array.Sort(sorted);
        cancellationToken.ThrowIfCancellationRequested();
        var average = Mean(times, 0);
        var slowestCount = (int)Math.Ceiling(times.Count * 0.01);
        var slowestMean = Mean(sorted, sorted.Length - slowestCount);
        var averageFps = 1000 / average;
        var lowFps = 1000 / slowestMean;
        if (!double.IsFinite(averageFps) || !double.IsFinite(lowFps))
            throw new InvalidDataException("Los frame times son demasiado pequeños para calcular FPS finitos.");
        return new GamingSession
        {
            SourceFile = fullPath,
            CapturedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(fullPath), TimeSpan.Zero),
            ProcessName = processName,
            FrameCount = times.Count,
            AverageFps = averageFps,
            AverageFrameTimeMs = average,
            P95FrameTimeMs = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1],
            P99FrameTimeMs = sorted[(int)Math.Ceiling(sorted.Length * 0.99) - 1],
            OnePercentLowFps = lowFps,
            IgnoredRows = ignoredRows,
            TimingColumn = header[timingIndex],
            FrameTimesMs = times
        };
    }

    private static TextFieldParser CreateParser(TextReader reader, string delimiter)
    {
        var parser = new TextFieldParser(reader) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(delimiter);
        return parser;
    }

    private static int CountUnquoted(string text, char delimiter)
    {
        var count = 0;
        var quoted = false;
        foreach (var character in text)
        {
            if (character == '"') quoted = !quoted;
            else if (character == delimiter && !quoted) count++;
        }
        return count;
    }

    private static int FindColumn(string[] header, string name)
    {
        var index = -1;
        for (var i = 0; i < header.Length; i++)
        {
            if (!string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase)) continue;
            if (index >= 0) throw new InvalidDataException("La cabecera contiene una columna duplicada: " + name + ".");
            index = i;
        }
        return index;
    }

    private static bool TryParseTime(string field, out double value)
    {
        value = 0;
        if (field.Contains(','))
        {
            if (field.Contains('.') || field.Count(c => c == ',') > 1) return false;
            field = field.Replace(',', '.');
        }
        return double.TryParse(field, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value > 0;
    }


    private static double Mean(IReadOnlyList<double> values, int start)
    {
        var mean = 0d;
        for (var i = start; i < values.Count; i++) mean += (values[i] - mean) / (i - start + 1);
        return mean;
    }
}
