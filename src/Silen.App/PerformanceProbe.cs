using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Silen.App;

internal static class PerformanceProbe
{
    // Ventana real y diagnóstico de solo lectura, sin perfiles ni acciones de optimización.
    internal static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        using var model = new MainViewModel(Path.Combine(directory, "isolated-history"), new ViewModelServices());
        var window = new MainWindow(model, animateStartup: true) { ShowInTaskbar = false };
        window.Show(); window.Activate();
        try
        {
            model.ApplySnapshot(await new Silen.Windows.WindowsCollector().CollectAsync(null, CancellationToken.None), isCurrent: true);
            await Task.Delay(2300);
            using var process = Process.GetCurrentProcess();
            var samples = new List<object>();
            async Task Measure(string mode, int milliseconds)
            {
                process.Refresh(); var began = process.TotalProcessorTime; var clock = Stopwatch.StartNew();
                var active = window.IsActive; var frames = window.MotionDiagnostics.RenderedFrames;
                await Task.Delay(milliseconds);
                process.Refresh();
                samples.Add(new
                {
                    Mode = mode, DurationMilliseconds = clock.Elapsed.TotalMilliseconds,
                    CpuPercentOfMachine = 100 * (process.TotalProcessorTime - began).TotalMilliseconds / clock.Elapsed.TotalMilliseconds / Environment.ProcessorCount,
                    WorkingSetMiB = process.WorkingSet64 / 1048576d, PrivateMiB = process.PrivateMemorySize64 / 1048576d,
                    ManagedMiB = GC.GetTotalMemory(false) / 1048576d,
                    ActiveForSample = active && window.IsActive,
                    RenderedFrames = window.MotionDiagnostics.RenderedFrames - frames,
                    SynchronousFrameUpdateMaxMilliseconds = window.MotionDiagnostics.MaximumRenderMilliseconds
                });
            }
            await Measure("Visible", 5000);
            window.WindowState = WindowState.Minimized; await Task.Delay(500);
            await Measure("Minimizada", 3000);
            await File.WriteAllTextAsync(Path.Combine(directory, "performance.json"), JsonSerializer.Serialize(new
            {
                Samples = samples, AppliedSystemChanges = false, ApiRequests = 0,
                Limits = "Muestras cortas de un único equipo tras diagnóstico local de solo lectura. CPU normalizada a todos los núcleos. No mide uso GPU ni costo completo del compositor. No se iniciaron perfiles del temporizador ni consultas de IA."
            }, Silen.Core.ReportStore.JsonOptions));
        }
        finally { await model.PrepareToCloseAsync(); window.Close(); }
    }
}
