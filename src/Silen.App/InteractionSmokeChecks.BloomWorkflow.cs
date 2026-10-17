using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Silen.Core;
using Silen.Windows;

namespace Silen.App;

internal static partial class InteractionSmokeChecks
{
    private static async Task RunBloomWorkflowChecksAsync(string directory, List<string> checks)
    {
        await VerifyBloomStartupAsync(directory, checks);
        await VerifyBloomOptimizationOutcomeAsync(directory, checks);
        var activity = new BloomActivityViewModel();
        activity.Begin("Limpiando temporales del usuario…");
        activity.Report("Estoy retirando temporales.", "Temporales de tu usuario · 2 de 8 revisados");
        if (!activity.IsWorking || !activity.IsVisible || !activity.Detail.Contains("2 de 8")) throw new InvalidOperationException("Bloom debe informar el trabajo real mientras está en curso.");
        activity.Dismiss();
        if (!activity.IsVisible) throw new InvalidOperationException("El trabajo en curso no se puede ocultar como si hubiera terminado.");
        activity.DetailResult("He detenido la limpieza.", "2 retirados · 6 sin retirar. En recuperación: 20 B.");
        activity.Finish(false, true);
        if (activity.IsWorking || !activity.Detail.Contains("6 sin retirar")) throw new InvalidOperationException("Cancelar conserva el resultado parcial sin fingir que se completó.");
        activity.Dismiss();
        if (activity.IsVisible) throw new InvalidOperationException("El resultado se cierra explícitamente.");
        checks.Add("Bloom informa pasos reales, conserva el resultado parcial y distingue trabajo de finalización");
        var deliveries = 0;
        using (var progress = new LatestCleanupProgress(_ => deliveries++))
        {
            await Task.Run(() => { for (var index = 0; index < 5000; index++) progress.Report(new(index, 5000, index, 0, 0, "Temporales de tu usuario")); });
            progress.Dispose();
            var before = deliveries;
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            if (deliveries != before) throw new InvalidOperationException("Un callback de limpieza cerrado no puede cambiar el siguiente estado.");
        }
        checks.Add("Progreso de limpieza coalescido: no encola una actualización por archivo ni entrega callbacks después del cierre");
        using var model = new MainViewModel(Path.Combine(directory, "bloom-workflow-history"), new ViewModelServices
        { CreateNativeTools = () => new FixtureOptimizationService() });
        model.ApplySnapshot(new SystemSnapshot { Samples = [new() { MemoryPercent = 40 }] }, isCurrent: true);
        var window = new MainWindow(model) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show();
            foreach (var page in new[] { 0, 1, 2, 4, 5 })
            {
                model.SelectedPage = page;
                await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
                if (Visuals<Button>(window).Any(button => button.IsVisible && button.Command == model.AppUpdate.CheckCommand))
                    throw new InvalidOperationException("Las actualizaciones y su error solo deben aparecer en Ajustes, no en las demás vistas.");
            }
            model.SelectedPage = 3;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
            if (!Visuals<Button>(window).Any(button => button.IsVisible && button.Command == model.AppUpdate.CheckCommand))
                throw new InvalidOperationException("La búsqueda manual de actualizaciones debe seguir disponible en Ajustes.");
            checks.Add("Versión, búsqueda de actualización y error solo en Ajustes; accesibles en modo normal");
            await model.Tools.RefreshAsync();
            await model.Settings.ToggleExpertAsync(); model.Settings.OpenManualCommand.Execute(null);
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
            if (!model.IsOptimizationDialogOpen || !model.IsManualDialogOpen || ((FrameworkElement)window.FindName("ApplicationChrome")).IsEnabled || !((FrameworkElement)window.FindName("OptimizationDialog")).IsVisible)
                throw new InvalidOperationException("El manual debe abrir una selección modal y bloquear la vista de fondo.");
            if (Visuals<Button>(window).Count(button => button.IsVisible && button.Command == model.Tools.ApplyManualCommand) != 1)
                throw new InvalidOperationException("El diálogo manual debe tener un único Aplicar.");
            await CaptureDialogAsync("manual");
            model.CloseOptimizationDialogCommand.Execute(null);
            await model.PcOptimization.PrepareAsync();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
            if (!model.IsPcDialogOpen || model.IsManualDialogOpen || Visuals<Button>(window).Count(button => button.IsVisible && button.Command == model.PcOptimization.ApplyCommand) != 1)
                throw new InvalidOperationException("Optimizar PC debe abrir su propio diálogo con un único Aplicar.");
            await CaptureDialogAsync("pc");
            model.CloseOptimizationDialogCommand.Execute(null);
            model.SelectedPage = 3; model.Activity.Begin("Limpiando temporales del usuario…");
            model.Activity.Report("Estoy retirando temporales.", "Temporales de tu usuario · 3 de 10 revisados · 2 archivos retirados. Se conservan para recuperación.");
            await CaptureDialogAsync("limpieza-simulada");
            model.Activity.Finish(true, false); model.Activity.Dismiss();
            checks.Add("Manual y Optimizar PC abren selecciones modales excluyentes; un Aplicar por selección y cierre sin cambios");
        }
        finally { await model.PrepareToCloseAsync(); window.Close(); ThemeManager.Apply("Claro"); }
        async Task CaptureDialogAsync(string state)
        {
            foreach (var theme in new[] { "Claro", "Oscuro" })
            foreach (var size in new[] { new Size(1240, 820), new Size(1000, 680) })
            {
                ThemeManager.Apply(theme); window.Width = size.Width; window.Height = size.Height; window.CaptureMotionAt(1);
                await RenderFixtureWindowAsync(window, Path.Combine(directory, $"bloom-{state}-{theme}-{size.Width}.png"));
                if (model.IsOptimizationDialogOpen)
                {
                    var command = model.IsManualDialogOpen ? model.Tools.ApplyManualCommand : model.PcOptimization.ApplyCommand;
                    var apply = Visuals<Button>((DependencyObject)window.FindName("OptimizationDialog")).Single(button => button.IsVisible && button.Command == command);
                    if (!FitsWindow(window, apply)) throw new InvalidOperationException("Aplicar debe quedar completo en el diálogo, en ambos tamaños y temas.");
                }
            }
        }
    }

    private static async Task VerifyBloomOptimizationOutcomeAsync(string directory, List<string> checks)
    {
        foreach (var result in new[]
        {
            new PcOptimizationResult([], true),
            new PcOptimizationResult([new("fixture", "Prueba", false, false, false, "No se aplicó")], false),
            new PcOptimizationResult([new("fixture", "Prueba", false, true, false, "Incierta", ChangeVerification.Uncertain)], false)
        })
        {
            using var model = new MainViewModel(Path.Combine(directory, "bloom-outcome-" + Guid.NewGuid().ToString("N")), new ViewModelServices
            {
                CreateNativeTools = () => new FixtureOptimizationService(), CreatePcOptimization = _ => new BloomOutcomeFixture(result),
                CreateRestorePoint = () => new FixtureRestorePointService(), ConfirmPcOptimization = _ => true
            });
            model.ApplySnapshot(new SystemSnapshot(), true);
            await model.PcOptimization.PrepareAsync(); await model.PcOptimization.ApplyAsync();
            if (model.Activity.IsWorking || !model.Activity.IsVisible || model.Activity.Title == "He terminado la revisión.")
                throw new InvalidOperationException("Un resultado cancelado, fallido o incierto debe llegar a Bloom sin anunciar éxito genérico.");
            if (result.Cancelled && model.Activity.Title != "He detenido la optimización.") throw new InvalidOperationException("Una cancelación retornada sin excepción debe ser explícita.");
            if (result.Uncertain > 0 && (!model.Activity.Detail.Contains("1 verificación incierta") || !model.Activity.Detail.Contains("0 cambios completados")))
                throw new InvalidOperationException("Una verificación incierta nunca se cuenta como cambio completado.");
        }
        checks.Add("Bloom refleja cancelación retornada, pasos fallidos y verificación incierta sin anunciar éxito genérico");
    }

    private sealed class BloomOutcomeFixture(PcOptimizationResult result) : IPcOptimizationService
    {
        public Task<PcOptimizationPreview> PreviewAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PcOptimizationPreview([new("fixture", "Prueba", "Servicio simulado", "Prueba", true, true, false, "Disponible")]));
        public Task<PcOptimizationResult> ApplyAsync(IReadOnlyCollection<string> ids, IProgress<PcOptimizationProgress>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private static async Task VerifyBloomStartupAsync(string directory, List<string> checks)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProgress<ScanProgress>? progress = null;
        var snapshot = new SystemSnapshot { Machine = new() { Manufacturer = "Marca", Model = "Modelo", DeviceKind = DeviceKind.Laptop, CpuName = "CPU de prueba", TotalMemoryGb = 16 }, Samples = [new() { CpuPercent = 20, MemoryPercent = 40 }] };
        using var model = new MainViewModel(Path.Combine(directory, "bloom-inicio"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            Collect = async (_, token, detailed) => { progress = detailed; entered.TrySetResult(); await release.Task.WaitAsync(token); return snapshot; },
            FindOem = (_, _, _) => Task.FromResult(new OemSupportResult(false, "Prueba", "", "", [], "Sin catálogo", false))
        });
        var window = new MainWindow(model, animateStartup: true) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        window.Show(); var initialization = model.InitializeAsync(true);
        try
        {
            await entered.Task;
            progress!.Report(new("Equipo", "Vista previa", 0, 14, Machine: snapshot.Machine));
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
            if (!model.ShowStartupLoading || model.Activity.IsVisible) throw new InvalidOperationException("El arranque muestra solo Bloom; una vista previa no basta para retirar la carga.");
            progress.Report(new("Equipo", "Identidad leída", 1, 14, Machine: snapshot.Machine));
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
            if (!model.ShowStartupLoading) throw new InvalidOperationException("Debe esperar la primera lectura de sensores, además de la identidad.");
            foreach (var theme in new[] { "Claro", "Oscuro" })
            foreach (var size in new[] { new Size(1240, 820), new Size(1000, 680) })
            {
                ThemeManager.Apply(theme); window.Width = size.Width; window.Height = size.Height; window.CaptureMotionAt(1);
                await RenderFixtureWindowAsync(window, Path.Combine(directory, $"bloom-carga-{theme}-{size.Width}.png"));
            }
            progress.Report(new("Recursos", "Primera muestra", 5, 14, snapshot.Samples[0]));
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
            if (model.ShowStartupLoading || !model.IsScanning || model.CanApplyChanges || model.CpuTemperature != "No disponible" || model.MachineTitle != "Marca Modelo")
                throw new InvalidOperationException("La primera lectura revela equipo, CPU y RAM, dice No disponible para sensores ausentes y mantiene Aplicar bloqueado.");
            release.TrySetResult(); await initialization;
            if (!model.CanApplyChanges || model.ShowStartupLoading || model.Activity.IsVisible) throw new InvalidOperationException("El diagnóstico completo habilita aplicar, sin burbujas de tareas internas de inicio.");
            checks.Add("Inicio espera identidad enriquecida y primera muestra; temperaturas ausentes explícitas, sin esperar las diez muestras para mostrar el equipo");
        }
        finally { release.TrySetResult(); await initialization; await model.PrepareToCloseAsync(); window.Close(); ThemeManager.Apply("Claro"); }
        foreach (var cancel in new[] { false, true })
        {
            using var failed = new MainViewModel(Path.Combine(directory, "bloom-fallo-" + cancel), new ViewModelServices
            {
                CreateNativeTools = () => new FixtureOptimizationService(),
                Collect = (_, _, _) => cancel ? Task.FromCanceled<SystemSnapshot>(new CancellationToken(true)) : Task.FromException<SystemSnapshot>(new UnauthorizedAccessException("Fuente no disponible"))
            });
            failed.BeginStartupPresentation(); await failed.InitializeAsync(true);
            if (failed.ShowStartupLoading || failed.CanApplyChanges || !failed.ShowRetryDetection) throw new InvalidOperationException("Fallo y cancelación deben retirar Bloom, informar el problema y permitir reintento sin acciones habilitadas.");
        }
        checks.Add("Inicio fallido o cancelado retira la carga y ofrece reintentar, sin quedar bloqueado esperando sensores");
    }
}
