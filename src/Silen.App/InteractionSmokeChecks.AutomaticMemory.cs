using System.IO;
using System.Windows.Threading;
using Silen.Core;

namespace Silen.App;

internal static partial class InteractionSmokeChecks
{
    private static async Task RunAutomaticMemoryChecksAsync(string directory, Dispatcher dispatcher, List<string> checks)
    {
        var scanStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishScan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oemStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishOem = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fastReads = 0; var pagefileReads = 0; var processReads = 0;
        long available = 6L << 30;
        var snapshot = new SystemSnapshot { Processes = [new("Proceso del análisis", 707, 512)] };
        using var model = new MainViewModel(Path.Combine(directory, "automatic-memory-history"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            ReadMemory = automatic => { fastReads++; return new(16L << 30, available, 12L << 30, 24L << 30, automatic); },
            ReadAutomaticPagefile = _ => { pagefileReads++; return Task.FromResult<bool?>(true); },
            ReadMemoryProcesses = _ => { processReads++; return Task.FromResult<IReadOnlyList<Silen.Core.ProcessInfo>>([new("Proceso actualizado", 808, 768)]); },
            Collect = async (_, token, _) => { scanStarted.TrySetResult(); await finishScan.Task.WaitAsync(token); return snapshot; },
            FindOem = async (_, _, token) =>
            {
                oemStarted.TrySetResult(); await finishOem.Task.WaitAsync(token);
                return new(false, "Prueba", "", "", [], "Sin consulta externa", false);
            }
        });
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException("RAM automática: " + message); }
        var initialization = model.InitializeAsync(autoScan: true);
        try
        {
            await scanStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Require(model.IsBusy && model.RamNetwork.HasMemory && fastReads > 0,
                "la primera lectura nativa debe estar disponible durante el diagnóstico sin pulsar Actualizar");
            Require(pagefileReads == 0 && processReads == 0,
                "el arranque rápido no debe consultar WMI de paginación ni enumerar procesos mientras reúne las primeras muestras");
            var previousReads = fastReads;
            available = 4L << 30; model.SelectedPage = 1;
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            Require(fastReads > previousReads && model.RamNetwork.PhysicalMemoryValue.StartsWith("12,0"),
                "entrar a RAM durante IsBusy debe renovar solo los contadores nativos");
            Require(pagefileReads == 0 && processReads == 0 && !model.RamNetwork.RefreshCommand.CanExecute(null),
                "la lectura automática conserva el bloqueo del refresco manual y no inicia fuentes lentas");

            model.ApplySnapshot(snapshot, isCurrent: true);
            Require(model.RamNetwork.Processes.Single().Id == 707,
                "los procesos del diagnóstico actual deben poblar la vista sin una consulta adicional");
            model.ApplySnapshot(new SystemSnapshot { Processes = [new("Proceso histórico", 999, 128)] });
            Require(model.RamNetwork.Processes.All(process => process.Id != 999),
                "un informe histórico no debe presentarse como procesos de la memoria actual");
            finishScan.TrySetResult();
            await oemStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var detailsBeforeOem = (pagefileReads, processReads);
            previousReads = fastReads; model.SelectedPage = 0; available = 3L << 30; model.SelectedPage = 1;
            Require(model.IsBusy && fastReads > previousReads && model.RamNetwork.PhysicalMemoryValue.StartsWith("13,0") &&
                detailsBeforeOem == (pagefileReads, processReads),
                "la consulta OEM no debe impedir lecturas rápidas ni disparar otra carga de detalles");
            checks.Add("RAM obtiene su primera lectura automáticamente y sigue disponible durante diagnóstico y consulta OEM");
            checks.Add("Los contadores rápidos no ejecutan paginación ni procesos; los procesos actuales llegan del diagnóstico sin usar informes históricos");
        }
        finally
        {
            finishScan.TrySetResult(); finishOem.TrySetResult();
            await initialization; await model.PrepareToCloseAsync();
        }

        await VerifyIndependentMemorySourcesAsync(directory, checks);
        await VerifyMemoryDetailsCancellationAsync(directory, checks);
    }

    private static async Task VerifyIndependentMemorySourcesAsync(string directory, List<string> checks)
    {
        var detailsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDetails = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pagefileFails = false; var processesFail = false; var fastFails = false;
        using var model = new MainViewModel(Path.Combine(directory, "independent-memory-history"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            ReadMemory = automatic => fastFails ? throw new InvalidOperationException("Contadores de prueba no disponibles") : new(16L << 30, 8L << 30, 10L << 30, 24L << 30, automatic),
            ReadAutomaticPagefile = async token =>
            {
                detailsStarted.TrySetResult(); await releaseDetails.Task.WaitAsync(token);
                if (pagefileFails) throw new InvalidOperationException("Paginación de prueba no disponible");
                return true;
            },
            ReadMemoryProcesses = _ => processesFail
                ? Task.FromException<IReadOnlyList<Silen.Core.ProcessInfo>>(new InvalidOperationException("Procesos de prueba no disponibles"))
                : Task.FromResult<IReadOnlyList<Silen.Core.ProcessInfo>>([new("Proceso válido", 909, 256)])
        });
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Fuentes de RAM: " + message); }
        var refresh = model.RamNetwork.RefreshAsync();
        try
        {
            await detailsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Require(model.RamNetwork.HasMemory && model.RamNetwork.IsRefreshing && !refresh.IsCompleted,
                "una fuente de detalles lenta no debe retrasar los contadores de RAM");
            releaseDetails.TrySetResult(); await refresh;
            pagefileFails = true; await model.RamNetwork.RefreshAsync();
            Require(model.RamNetwork.HasMemory && model.RamNetwork.HasProcesses && model.RamNetwork.HasMemoryError &&
                model.RamNetwork.PagefileState.Contains("sin comprobar") && model.RamNetwork.MemoryError.Contains("paginación", StringComparison.OrdinalIgnoreCase),
                "fallar la paginación debe conservar RAM y procesos válidos e identificar la fuente que falló");
            pagefileFails = false; processesFail = true; await model.RamNetwork.RefreshAsync();
            Require(model.RamNetwork.HasMemory && !model.RamNetwork.HasProcesses && model.RamNetwork.PagefileState.Contains("administrada") &&
                model.RamNetwork.MemoryError.Contains("procesos", StringComparison.OrdinalIgnoreCase),
                "fallar procesos debe retirar su lista sin borrar los contadores ni la paginación válidos");
            fastFails = true; model.RamNetwork.UpdateLive();
            Require(!model.RamNetwork.HasMemory && model.RamNetwork.PhysicalMemoryValue == "No disponible" && !model.RamNetwork.Assessment.Pressure,
                "una lectura nativa fallida debe retirar cifras y muestras anteriores sin propagar la excepción al temporizador");
            fastFails = false; processesFail = false; await model.RamNetwork.RefreshAsync();
            Require(model.RamNetwork.HasMemory && model.RamNetwork.HasProcesses && !model.RamNetwork.HasMemoryError,
                "recuperar las tres fuentes debe retirar los errores anteriores");
            checks.Add("La carga lenta de detalles no retrasa RAM; cada fuente puede fallar y recuperarse sin borrar las demás ni mostrar cifras obsoletas");
        }
        finally { releaseDetails.TrySetResult(); await refresh; await model.PrepareToCloseAsync(); }
    }

    private static async Task VerifyMemoryDetailsCancellationAsync(string directory, List<string> checks)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var firstCancellation = new CancellationTokenSource();
        using var waitingCancellation = new CancellationTokenSource();
        var pagefileReads = 0; var block = true;
        using var model = new MainViewModel(Path.Combine(directory, "memory-cancellation-history"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            ReadMemory = automatic => new(16L << 30, 8L << 30, 10L << 30, 24L << 30, automatic),
            ReadAutomaticPagefile = async token =>
            {
                pagefileReads++; started.TrySetResult();
                if (block) await Task.Delay(Timeout.Infinite, token);
                return true;
            },
            ReadMemoryProcesses = _ => Task.FromResult<IReadOnlyList<Silen.Core.ProcessInfo>>([])
        });
        static async Task RequireCancellation(Task task)
        {
            try { await task; }
            catch (OperationCanceledException) { return; }
            throw new InvalidOperationException("Los detalles de RAM deben respetar la cancelación.");
        }
        var first = model.RamNetwork.RefreshDetailsCoreAsync(firstCancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var waiting = model.RamNetwork.RefreshDetailsCoreAsync(waitingCancellation.Token);
            waitingCancellation.Cancel(); await RequireCancellation(waiting);
            if (pagefileReads != 1 || !model.RamNetwork.IsRefreshing)
                throw new InvalidOperationException("Una carga de detalles en espera no debe ejecutar otra consulta ni retirar el estado de la carga activa.");
            firstCancellation.Cancel(); await RequireCancellation(first);
            if (model.RamNetwork.IsRefreshing || !model.RamNetwork.HasMemory)
                throw new InvalidOperationException("Cancelar los detalles debe liberar el indicador y conservar la lectura rápida válida.");
            block = false; await model.RamNetwork.RefreshDetailsCoreAsync(CancellationToken.None);
            if (pagefileReads != 2 || model.RamNetwork.IsRefreshing)
                throw new InvalidOperationException("Cancelar debe liberar el semáforo para permitir la próxima lectura de detalles.");
            checks.Add("Las consultas de detalles se serializan y cancelan sin duplicar WMI, bloquear el reintento ni borrar la lectura rápida");
        }
        finally
        {
            firstCancellation.Cancel();
            try { await first; } catch (OperationCanceledException) { }
            await model.PrepareToCloseAsync();
        }
    }
}
