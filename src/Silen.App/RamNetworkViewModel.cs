using System.Collections.ObjectModel;
using System.Windows.Input;
using Silen.Core;
using Silen.Windows;

namespace Silen.App;

public sealed record DnsRowDisplay(DnsBenchmarkRow Row)
{
    public string Name => Row.Name;
    public string Address => Row.Address;
    public string Median => Row.MedianMs is { } value ? value.ToString("N1", Labels.Culture) + " ms" : Row.Completed == 0 ? "Pendiente" : "Sin respuesta válida";
    public string Last => Row.LastMs is { } value ? value.ToString("N1", Labels.Culture) + " ms" : Row.Completed == 0 ? "Pendiente" : "Sin respuesta";
    public string Progress => $"{Row.Completed}/3 · {SpanishText.Count(Row.Failed, "fallo", "fallos")}";
}

public sealed class RamNetworkViewModel : ObservableObject
{
    private readonly ViewModelServices sources;
    private readonly Func<string, Func<CancellationToken, Task>, Task<bool>> run;
    private readonly SemaphoreSlim detailsGate = new(1, 1);
    private readonly List<MemoryObservation> samples = [];
    private MemoryObservation? current;
    private bool? automaticPagefile;
    private string dnsStatus = "Aún no hay mediciones DNS.", memoryStatus = "Actualiza los detalles para revisar procesos y paginación.";
    private string readingError = "", pagefileError = "", processesError = "", dnsError = "";
    private bool measuring, refreshing, dnsAttempted;
    private int generation;
    internal RamNetworkViewModel(ViewModelServices sources, Func<string, Func<CancellationToken, Task>, Task<bool>> run, Func<bool> canRun)
    {
        this.sources = sources; this.run = run;
        RefreshCommand = new AsyncCommand(_ => RefreshAsync(), _ => canRun(), ex => { memoryStatus = "No se pudo leer la memoria: " + ex.Message; Changed(null); });
        MeasureCommand = new AsyncCommand(_ => MeasureAsync(), _ => canRun(), ex => { dnsStatus = "No se pudo completar la medición: " + ex.Message; Changed(null); });
    }
    public ICommand RefreshCommand { get; }
    public ICommand MeasureCommand { get; }
    public ObservableCollection<Silen.Core.ProcessInfo> Processes { get; } = [];
    public ObservableCollection<DnsRowDisplay> DnsRows { get; } = [];
    public RamAssessment Assessment => RamAssessment.Evaluate(samples);
    public bool HasMemory => current is not null;
    public bool HasProcesses => Processes.Count > 0;
    public bool HasDnsRows => DnsRows.Count > 0;
    public string DnsEmptyMessage => dnsAttempted ? "No se obtuvieron resultados. Puedes repetir la medición." : "Sin mediciones todavía. Pulsa Medir DNS para comparar los servidores.";
    public bool HasMemoryError => MemoryError.Length > 0;
    public bool HasDnsError => dnsError.Length > 0;
    public bool IsRefreshing => refreshing;
    public string MemoryError => string.Join(" ", new[] { readingError, pagefileError, processesError }.Where(error => error.Length > 0));
    public string DnsError => dnsError;
    public string AssessmentLabel => Assessment.Pressure ? "Presión de memoria sostenida · ver detalles" : "Interpretar estas lecturas";
    public string PhysicalMemoryValue => current is null ? "No disponible" : $"{GiB(current.TotalBytes - current.AvailableBytes)} / {GiB(current.TotalBytes)} GiB";
    public string CommittedMemoryValue => current is null ? "No disponible" : $"{GiB(current.CommitBytes)} / {GiB(current.CommitLimitBytes)} GiB";
    public string PhysicalMemory => current is null ? "No disponible" : $"{GiB(current.TotalBytes - current.AvailableBytes)} / {GiB(current.TotalBytes)} GiB en uso";
    public string AvailableMemory => current is null ? "Sin medición" : $"{GiB(current.AvailableBytes)} GiB disponibles · {current.UsedPercent.ToString("N0", Labels.Culture)} % en uso";
    public string CommittedMemory => current is null ? "No disponible" : $"{GiB(current.CommitBytes)} / {GiB(current.CommitLimitBytes)} GiB comprometidos";
    public string PagefileState => automaticPagefile switch { true => "Paginación administrada por Windows", false => "Paginación configurada manualmente", _ => "Configuración de paginación sin comprobar" };
    public string MemoryStatus => memoryStatus;
    public string DnsStatus => dnsStatus;
    public bool IsMeasuring => measuring;
    public string MeasurementTime => current?.At?.ToLocalTime().ToString("HH:mm:ss", Labels.Culture) is { } time ? "Lectura " + time : "Esperando una lectura real";
    internal void InitializeReading()
    {
        if (current is null) UpdateLive();
    }
    internal void ApplyCurrentSnapshot(SystemSnapshot snapshot)
    {
        SetProcesses(snapshot.Processes);
        processesError = "";
        UpdateLive();
    }
    internal void UpdateLive()
    {
        readingError = "";
        try
        {
            current = sources.ReadMemory(automaticPagefile);
            if (current is not { TotalBytes: > 0, AvailableBytes: >= 0, CommitBytes: >= 0, CommitLimitBytes: > 0 } ||
                current.AvailableBytes > current.TotalBytes || current.CommitBytes > current.CommitLimitBytes)
            {
                current = null;
                readingError = "No se pudo obtener una lectura actual de los contadores de memoria. Se volverá a intentar automáticamente.";
            }
        }
        catch (Exception ex)
        {
            current = null;
            readingError = "No se pudieron leer los contadores de memoria. " + ex.Message;
        }
        if (current is null) samples.Clear();
        else
        {
            current = current with { At = current.At ?? DateTimeOffset.Now };
            samples.Add(current); if (samples.Count > 5) samples.RemoveAt(0);
        }
        Changed(null);
    }
    internal Task<bool> RefreshAsync() => run("Revisando RAM, procesos y paginación…", RefreshDetailsCoreAsync);
    internal async Task RefreshDetailsCoreAsync(CancellationToken token)
    {
        await detailsGate.WaitAsync(token);
        refreshing = true; pagefileError = ""; processesError = ""; Changed(null);
        try
        {
            UpdateLive();
            await Task.WhenAll(ReadPagefileAsync(token), ReadProcessesAsync(token));
            token.ThrowIfCancellationRequested();
            UpdateLive();
        }
        catch (OperationCanceledException) { memoryStatus = "Actualización cancelada. Los detalles anteriores pueden estar desactualizados."; throw; }
        finally { refreshing = false; detailsGate.Release(); Changed(null); Changed(nameof(MemoryStatus)); }
    }
    private async Task ReadPagefileAsync(CancellationToken token)
    {
        try
        {
            var result = await sources.ReadAutomaticPagefile(token);
            token.ThrowIfCancellationRequested();
            automaticPagefile = result;
            pagefileError = result is null ? "No se pudo comprobar la configuración de paginación. Windows no devolvió ese dato." : "";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { automaticPagefile = null; pagefileError = "No se pudo comprobar la paginación. " + ex.Message; }
        Changed(null);
    }
    private async Task ReadProcessesAsync(CancellationToken token)
    {
        try
        {
            var processes = await sources.ReadMemoryProcesses(token);
            token.ThrowIfCancellationRequested();
            SetProcesses(processes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Processes.Clear();
            processesError = "No se pudieron leer los procesos por memoria. " + ex.Message;
            memoryStatus = "Los procesos no están disponibles. Puedes actualizar los detalles para reintentar.";
        }
        Changed(null);
    }
    private void SetProcesses(IReadOnlyList<Silen.Core.ProcessInfo> processes)
    {
        Processes.Clear(); foreach (var process in processes) Processes.Add(process);
        memoryStatus = processes.Count == 0 ? "No se obtuvieron procesos en esta lectura. Puedes actualizar los detalles para reintentar."
            : "Memoria de trabajo por proceso. Las páginas compartidas pueden contarse en más de un proceso; no sumes la columna como uso total de RAM. No se cerró ninguna aplicación.";
    }
    internal Task<bool> MeasureAsync() => run("Midiendo consultas DNS reales…", async token =>
    {
        DnsRows.Clear(); dnsError = ""; measuring = true; dnsAttempted = true; var thisGeneration = ++generation; Changed(null);
        var progress = new Progress<DnsBenchmarkRow>(row =>
        {
            if (!measuring || generation != thisGeneration) return;
            var old = DnsRows.FirstOrDefault(item => item.Address == row.Address); if (old is null) return;
            DnsRows[DnsRows.IndexOf(old)] = new(row);
        });
        try
        {
            var resolvers = sources.GetDnsResolvers();
            foreach (var resolver in resolvers) DnsRows.Add(new(new(resolver.Name, resolver.Address, 0, 0, null, null, DateTimeOffset.Now)));
            dnsStatus = $"Midiendo {SpanishText.Count(resolvers.Count, "servidor", "servidores")} · tres consultas por servidor."; Changed(null);
            var rows = await sources.MeasureDns(resolvers, progress, token); token.ThrowIfCancellationRequested();
            DnsRows.Clear(); foreach (var row in rows) DnsRows.Add(new(row));
            var best = rows.FirstOrDefault(row => row.Reliable);
            dnsStatus = best is null ? "No hubo un servidor con tres respuestas válidas. No se recomienda un ganador; puede haber bloqueos o pérdidas de red."
                : $"Menor mediana sin fallos: {best.Name} ({best.Address}), {best.MedianMs!.Value.ToString("N1", Labels.Culture)} ms. Medido a las {DateTime.Now.ToString("HH:mm:ss", Labels.Culture)}. Repite para contrastar.";
        }
        catch (OperationCanceledException) { dnsStatus = "Medición cancelada. Los resultados parciales no eligen un ganador."; throw; }
        catch (Exception ex) { dnsError = "No se pudo completar la medición DNS. " + ex.Message; dnsStatus = "Puedes repetir la medición. Los datos parciales no permiten elegir un servidor."; }
        finally { measuring = false; generation++; Changed(null); Changed(nameof(DnsStatus)); }
    });
    private static string GiB(long bytes) => (bytes / 1073741824d).ToString("N1", Labels.Culture);
}
