using System.IO;
using System.Text.Json;
using Silen.Core;

namespace Silen.App;

internal static partial class InteractionSmokeChecks
{
    private static async Task RunStartupDiagnosticsChecksAsync(string directory, List<string> checks)
    {
        using (var model = new MainViewModel(Path.Combine(directory, "first-frame"), new ViewModelServices { CreateNativeTools = () => new FixtureOptimizationService() }))
        {
            model.BeginStartupPresentation();
            var window = new MainWindow(model, animateStartup: true);
            try
            {
                window.CaptureMotionAt(0);
                var chrome = (System.Windows.FrameworkElement)window.FindName("ApplicationChrome");
                if (!model.ShowStartupLoading || chrome.IsHitTestVisible)
                    throw new InvalidOperationException("Bloom debe ocupar la carga inicial hasta tener identidad y primera muestra, sin permitir acciones sobre datos pendientes.");
            }
            finally { window.Close(); }
        }
        checks.Add("Bloom ocupa el primer fotograma hasta la primera lectura; no se presenta el equipo sin datos");
        await VerifyBasicPreviewAsync(directory);
        checks.Add("Modelo, CPU y RAM se muestran sin muestra ni GPU inventada; cancelar retira la vista previa y no permite aplicar");
        await VerifyDetectionPrecedesHistoryAsync(directory);
        checks.Add("El arranque detecta el equipo antes de cargar los informes anteriores; Aplicar sigue bloqueado durante la lectura parcial");
        for (var repetition = 0; repetition < 2; repetition++)
            await VerifyStartupDiagnosticsAsync(directory);
        checks.Add("Sonda optativa del arranque real: dos ejecuciones aisladas, fases completas y salida limitada a tiempos/contadores");
    }

    private static async Task VerifyBasicPreviewAsync(string directory)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = new MainViewModel(Path.Combine(directory, "preview-" + Guid.NewGuid().ToString("N")), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            Collect = async (_, token, progress) =>
            {
                progress?.Report(new("Equipo", "Vista previa local", 0, 14,
                    Machine: new() { Manufacturer = "Marca", Model = "Modelo", CpuName = "CPU actual", TotalMemoryGb = 16 }));
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                return new SystemSnapshot();
            }
        });
        var start = model.ScanAsync();
        try
        {
            await entered.Task;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            if (model.MachineTitle != "Marca Modelo" || model.CpuName != "CPU actual" || !model.MemoryDetail.Contains("16") ||
                model.GpuName != "Midiendo gráficos…" || model.SummaryCpuValue != "—" || model.ScanPercent != 0 ||
                model.HasCompletedResult || model.CanApplyChanges || model.PcOptimization.PrepareCommand.CanExecute(null))
                throw new InvalidOperationException("La vista previa básica debe informar sin inventar lecturas ni permitir aplicar.");
            var window = new MainWindow(model, animateStartup: true)
                { Width = 1240, Height = 820, ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
            try
            {
                window.Show();
                foreach (var theme in new[] { "Claro", "Oscuro" })
                {
                    ThemeManager.Apply(theme); window.CaptureMotionAt(0);
                    await RenderFixtureWindowAsync(window, Path.Combine(directory, $"inicio-vista-previa-{theme}.png"));
                }
            }
            finally { window.Close(); ThemeManager.Apply("Claro"); }
        }
        finally { model.CancelCommand.Execute(null); await start; }
        if (model.HasSnapshot || model.HasResourceData || model.CanApplyChanges)
            throw new InvalidOperationException("Cancelar debe retirar los datos de la vista previa, sin habilitar acciones.");
        await model.PrepareToCloseAsync();
    }

    private static async Task VerifyDetectionPrecedesHistoryAsync(string directory)
    {
        var history = Path.Combine(directory, "history-order-" + Guid.NewGuid().ToString("N"));
        await new ReportStore(history).SaveAsync(new SystemSnapshot { Machine = new() { CpuName = "CPU anterior" } });
        MainViewModel? observed = null;
        using var model = new MainViewModel(history, new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            Collect = (_, _, _) =>
            {
                if (observed!.Reports.Count != 0 || observed.CanApplyChanges)
                    throw new InvalidOperationException("El historial no debe bloquear la primera detección ni habilitar Aplicar.");
                return Task.FromResult(new SystemSnapshot { Machine = new() { CpuName = "CPU actual" } });
            },
            FindOem = (_, _, _) => Task.FromResult(new OemSupportResult(false, "Prueba", "", "", [], "Sin red en prueba", false))
        });
        observed = model;
        await model.InitializeAsync(autoScan: true);
        if (!model.HasCompletedResult || model.Snapshot?.Machine.CpuName != "CPU actual" || model.Reports.Count != 2)
            throw new InvalidOperationException("El arranque debe mostrar datos actuales y cargar el historial después del diagnóstico.");
        await model.PrepareToCloseAsync();
    }

    private static async Task VerifyStartupDiagnosticsAsync(string directory)
    {
        var output = Path.Combine(directory, "startup-diagnostics-fixture-" + Guid.NewGuid().ToString("N"));
        using var trace = StartupDiagnostics.Begin(output);
        using var model = new MainViewModel(Path.Combine(directory, "startup-diagnostics-history"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            Collect = (_, _, _) => Task.FromResult(new SystemSnapshot()),
            FindOem = (_, _, _) => Task.FromResult(new OemSupportResult(false, "Prueba", "", "", [], "Sin red en prueba", false))
        });
        await model.InitializeAsync(autoScan: true);
        await trace.SaveAsync(model);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "startup.json")));
        var phases = json.RootElement.GetProperty("Phases").EnumerateArray().Select(phase => phase.GetProperty("Name").GetString()).ToArray();
        if (!new[] { "InitializeAsync", "PreferencesAndHistory", "Collect", "OEM", "Tools", "WindowsUpdate", "Profiles" }.All(phases.Contains))
            throw new InvalidOperationException("La medición del arranque no cubre todas las fases reales de inicialización.");
        if (json.RootElement.GetProperty("ProductionEntryPoint").GetBoolean() || json.RootElement.TryGetProperty("Snapshot", out _) || json.RootElement.TryGetProperty("ApiKey", out _))
            throw new InvalidOperationException("La medición de arranque solo debe guardar tiempos y contadores, sin snapshot ni claves.");
        trace.Dispose();
        if (StartupDiagnostics.Measure("DisabledCheck") is not null)
            throw new InvalidOperationException("Al cerrar el diagnóstico no puede quedar un observador activo.");
        await model.PrepareToCloseAsync();
    }

}
