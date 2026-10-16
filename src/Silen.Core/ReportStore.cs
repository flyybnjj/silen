using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Silen.Core;

public sealed class ReportStore
{
    private readonly string directory;
    private readonly SemaphoreSlim actionGate = new(1, 1);
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ReportStore(string? directory = null)
    {
        this.directory = Path.GetFullPath(directory ?? Path.Combine(AppDataPaths.Root, "reports"));
    }

    public async Task<string> SaveAsync(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var stamp = snapshot.CapturedAt.UtcDateTime.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"{stamp}_{snapshot.Id:N}.json");
        await WriteAtomicallyAsync(path, JsonSerializer.Serialize(ReportPrivacy.ForSharing(snapshot), JsonOptions));
        return path;
    }

    public async Task<IReadOnlyList<SavedReport>> ListAsync()
    {
        if (!Directory.Exists(directory)) return [];
        var reports = new List<SavedReport>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(path).Equals("actions.json", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var snapshot = await LoadAsync(path);
                reports.Add(new(path, snapshot.CapturedAt, snapshot.Machine.CpuName, RecommendationEngine.Analyze(snapshot).Count));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {

            }
        }
        return reports.OrderByDescending(r => r.CapturedAt).ToArray();
    }

    public async Task<SystemSnapshot> LoadAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(fullPath), directory, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("El informe debe pertenecer al historial de esta aplicación.", nameof(path));
        try
        {
            var text = await ReadSharedTextAsync(fullPath);
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("Id", out _)
                || !document.RootElement.TryGetProperty("CapturedAt", out _))
                throw new InvalidDataException("El archivo no contiene un diagnóstico de Silen.");
            var result = JsonSerializer.Deserialize<SystemSnapshot>(text, JsonOptions);
            if (result is null || result.Id == Guid.Empty || result.Machine is null || result.Samples is null ||
                result.Drives is null || result.Drivers is null || result.Startup is null || result.Processes is null ||
                result.Networks is null || result.Notes is null || result.Samples.Any(s => s is null) ||
                result.Drives.Any(s => s is null) || result.Drivers.Any(s => s is null) ||
                result.Startup.Any(s => s is null) || result.Processes.Any(s => s is null) ||
                result.Networks.Any(s => s is null) || result.Notes.Any(s => s is null))
                throw new InvalidDataException("El informe está incompleto o dañado.");
            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("No se pudo leer el informe. El archivo JSON está dañado o tiene un formato incompatible.", ex);
        }
    }

    public async Task RecordActionAsync(ActionRecord record)
    {
        await actionGate.WaitAsync();
        try
        {
            Directory.CreateDirectory(directory);
            await using var lease = await AcquireActionLeaseAsync();
            var actions = (await ReadActionsWithoutLeaseAsync()).ToList();
            actions.Add(record);
            await WriteAtomicallyAsync(Path.Combine(directory, "actions.json"), JsonSerializer.Serialize(actions, JsonOptions));
        }
        finally { actionGate.Release(); }
    }

    public async Task<IReadOnlyList<ActionRecord>> GetActionsAsync()
    {
        var path = Path.Combine(directory, "actions.json");
        if (!File.Exists(path)) return [];
        await using var lease = await AcquireActionLeaseAsync();
        return await ReadActionsWithoutLeaseAsync();
    }

    private async Task<IReadOnlyList<ActionRecord>> ReadActionsWithoutLeaseAsync()
    {
        var path = Path.Combine(directory, "actions.json");
        if (!File.Exists(path)) return [];
        try
        {
            var records = JsonSerializer.Deserialize<List<ActionRecord>>(await ReadSharedTextAsync(path), JsonOptions);
            if (records is null || records.Any(record => record is null))
                throw new InvalidDataException("El registro de guías está incompleto. Se conservó el archivo existente.");
            return records;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("El registro de guías está dañado. Se conservó el archivo existente.", ex);
        }
    }

    private async Task<FileStream> AcquireActionLeaseAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var path = Path.Combine(directory, "actions.lock");
        while (true)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) when (ex.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021))
            {
                if (DateTime.UtcNow >= deadline)
                    throw new IOException("Otra instancia está escribiendo el historial. Vuelve a intentar guardar la guía.", ex);
                await Task.Delay(25);
            }
        }
    }

    private static async Task<string> ReadSharedTextAsync(string path)
    {

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private async Task WriteAtomicallyAsync(string path, string text)
    {
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, text);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
