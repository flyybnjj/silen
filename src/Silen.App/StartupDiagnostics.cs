using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Silen.App;

// Diagnóstico local optativo. Sin variable de entorno no observa ni escribe nada.
internal sealed class StartupDiagnostics : IDisposable
{
    internal const string EnvironmentVariable = "SILEN_STARTUP_DIAGNOSTICS_DIRECTORY";
    private static readonly AsyncLocal<StartupDiagnostics?> Current = new();
    private readonly string directory;
    private readonly bool productionEntry;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly double processOffset;
    private readonly List<object> phases = [];
    private readonly Dictionary<string, double> milestones = [];
    private readonly object gate = new();
    private readonly StartupDiagnostics? previous;
    private MainViewModel? model;
    private FrameworkElement? chrome;
    private DependencyPropertyDescriptor? opacity;
    private bool disposed;

    private StartupDiagnostics(string directory, bool productionEntry)
    {
        this.directory = Path.GetFullPath(directory);
        this.productionEntry = productionEntry;
        Directory.CreateDirectory(this.directory);
        using var process = Process.GetCurrentProcess();
        processOffset = Math.Max(0, (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalMilliseconds);
        previous = Current.Value; Current.Value = this;
    }

    internal static StartupDiagnostics Begin(string directory) => new(directory, false);
    internal static StartupDiagnostics? FromEnvironment() => Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } directory ? new(directory, true) : null;
    internal static IDisposable? Measure(string name) => Current.Value is { } trace ? new Phase(trace, name) : null;
    internal static async Task TrackAsync(string name, Func<Task> work) { using var phase = Measure(name); await work(); }
    private double Now => processOffset + clock.Elapsed.TotalMilliseconds;
    private void Mark(string name) { lock (gate) milestones.TryAdd(name, Now); }

    internal void Observe(MainViewModel model, MainWindow window)
    {
        this.model = model;
        chrome = (FrameworkElement)window.FindName("ApplicationChrome");
        opacity = DependencyPropertyDescriptor.FromProperty(UIElement.OpacityProperty, typeof(UIElement));
        opacity.AddValueChanged(chrome, OnOpacity);
        model.PropertyChanged += OnModel;
        Mark("WindowShown"); OnModel(null, new(null));
    }

    private void OnOpacity(object? sender, EventArgs args) => CheckVisibility();
    private void OnModel(object? sender, PropertyChangedEventArgs args)
    {
        if (model is null) return;
        if (model.HasMachineIdentity) Mark("Identity");
        if (model.MemoryDetail.Contains("instalados")) Mark("RamCapacity");
        if (model.DriveDetail.Contains("libres de")) Mark("Drives");
        if (model.MemoryTrace.Count > 0) Mark("FirstSample");
        if (model.MemoryTrace.Count == 10) Mark("TenSamples");
        if (model.HasCompletedResult) Mark("CompleteSnapshot");
        CheckVisibility();
    }

    private void CheckVisibility()
    {
        if (chrome is null || chrome.Opacity < .98) return;
        lock (gate)
            if (milestones.ContainsKey("Identity") && milestones.ContainsKey("RamCapacity") && !milestones.ContainsKey("FirstInformationVisibleLayout"))
            { chrome.UpdateLayout(); Mark("FirstInformationVisibleLayout"); }
        lock (gate)
            if (!milestones.ContainsKey("Identity") || !milestones.ContainsKey("RamCapacity") || !milestones.ContainsKey("Drives") || milestones.ContainsKey("BasicsVisibleLayout")) return;
        chrome.UpdateLayout(); Mark("BasicsVisibleLayout");
    }

    internal async Task SaveAsync(MainViewModel model)
    {
        Mark("AllStartupWork");
        // Lista explícita: no serializar modelo, rutas, errores de red ni claves.
        var result = new { ProductionEntryPoint = productionEntry, Success = model.HasCompletedResult,
            Origin = "Milisegundos desde Process.StartTime, con un offset UTC inicial y reloj monotónico para las fases; visibilidad exige opacidad ≥0,98 y layout, no presentación GPU.",
            Milestones = milestones, Phases = phases, Profiles = model.Settings.TimerProfiles.Count,
            GithubFailed = model.AppUpdate.HasError, Samples = model.Snapshot?.Samples.Count,
            SampleTimes = model.Snapshot?.Samples.Select(sample => sample.At).ToArray() };
        await using var stream = new FileStream(Path.Combine(directory, "startup.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, result, Silen.Core.ReportStore.JsonOptions);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (model is not null) model.PropertyChanged -= OnModel;
        if (chrome is not null) opacity?.RemoveValueChanged(chrome, OnOpacity);
        Current.Value = previous;
    }

    private sealed class Phase(StartupDiagnostics trace, string name) : IDisposable
    {
        private readonly double began = trace.Now;
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var ended = trace.Now;
            lock (trace.gate) trace.phases.Add(new { Name = name, BeganMs = began, EndMs = ended, DurationMs = ended - began });
        }
    }
}
