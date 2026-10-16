using System.ComponentModel;
using System.Security;

namespace Silen.Windows;

public sealed class PcOptimizationService(INativeOptimizationService native, Func<bool?>? onAcPower = null) : IPcOptimizationService
{
    private readonly Func<bool?> readAc = onAcPower ?? WindowsPowerStatus.ReadAc;
    private PcOptimizationPreview? reviewed;
    private Dictionary<string, StartupEntry> reviewedStartup = new(StringComparer.Ordinal);
    private string? applicationTarget;
    private int targetGeneration;
    private IApplicationTimerService timers = new InactiveApplicationTimerService();
    public void AttachTimerService(IApplicationTimerService service) { timers = service; reviewed = null; targetGeneration++; }
    public void SetApplicationTarget(string? executablePath) { applicationTarget = executablePath is null ? null : ApplicationGpuPreference.ValidateExecutable(executablePath); targetGeneration++; reviewed = null; }
    private static readonly Guid PerformanceMode = Guid.Parse("ded574b5-45a0-4f42-8737-46345c09c238");

    public async Task<PcOptimizationPreview> PreviewAsync(CancellationToken cancellationToken = default)
    {
        reviewed = null;
        reviewedStartup = new(StringComparer.Ordinal);
        var overview = await native.InspectAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var ac = readAc() == true;
        var options = new List<PcOptimizationOption>();
        if (applicationTarget is { } target)
        {
            var available = File.Exists(target);
            var highPerformance = available ? native.ReadApplicationGpuPreference(target) : null;
            options.Add(new("application-gpu", "Preferir GPU de alto rendimiento para " + Path.GetFileName(target),
                "Preferencia de Windows para " + target + ". No cambia archivos del producto. Windows y el hardware eligen la GPU efectiva; vuelve a abrir la app. Puede aumentar consumo. Estado anterior respaldado.", "Aplicación elegida", available, false, highPerformance == true,
                !available ? "El ejecutable ya no existe" : highPerformance == true ? "Ya tiene preferencia de alto rendimiento" : highPerformance == false ? "La preferencia actual permite un cambio" : "Sin preferencia concluyente; Windows decide la GPU efectiva"));
            var interval = timers.Inspect();
            options.Add(new("application-timer", "Temporizador mínimo por aplicación (experimental)",
                "Guarda un perfil para " + Path.GetFileName(target) + ". Mientras Silen esté abierto y ese ejecutable esté en marcha, solicita el mínimo que informa Windows" +
                (interval is null ? "." : $" ({interval.MinimumMilliseconds.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("es-CL"))} ms).") +
                " Se comprueba cada dos segundos y se libera la solicitud propia al terminar el producto o cerrar Silen. Puede aumentar consumo. Windows 11 no garantiza mejorar otros procesos ni reducir latencia de entrada. No cambia HPET, BCD ni drivers.",
                "Aplicación elegida", available && timers.Ready && interval is not null, false, timers.IsEnabled(target),
                timers.IsEnabled(target) ? "Perfil guardado; se activa con el ejecutable mientras Silen esté abierto" : timers.Ready && interval is not null ? "Opcional · solicita y verifica el mínimo disponible" : "Controlador de temporizador no disponible en esta sesión"));
        }
        foreach (var toggle in overview.Toggles)
        {
            if (applicationTarget is null && toggle.Id is "input-filterkeys" or "input-mouse-linear") continue;
            var off = toggle.Id is "xbox-captures" or "animations";
            var applied = toggle.Enabled == !off;
            var available = applied || (off ? toggle.CanDisable : toggle.CanEnable);
            if (toggle.Id == "power-throttling" && !ac && !applied) available = false;
            var recommended = toggle.Id == "game-mode" || toggle.Recommended;
            var title = toggle.Id switch { "xbox-captures" => "Desactivar capturas Xbox", "animations" => "Reducir animaciones de ventanas", _ => toggle.Name };
            options.Add(new(toggle.Id, title, toggle.Description, toggle.Category, available,
                available && !applied && recommended, applied, !available && toggle.Id == "power-throttling" && !ac ? "Conecta el equipo a corriente" : toggle.Status));
        }
        var platform = native as NativeOptimizationService ?? (native as SystemActionOptimizationService)?.Local;
        var mode = ac ? platform?.ReadPowerMode() : null;
        var scheme = overview.PowerSchemes.FirstOrDefault(item => item.Id.Equals(FortniteProfileService.HighPerformanceScheme, StringComparison.OrdinalIgnoreCase));
        options.Add(new("power-mode", "Mejor rendimiento con corriente", "Activa el modo de rendimiento de Windows. Puede aumentar consumo, temperatura y ruido. Guarda el modo anterior; requiere el plan Equilibrado compatible.", "Energía",
            mode is not null, mode is not null && mode != PerformanceMode, mode == PerformanceMode,
            !ac ? "Conecta el equipo a corriente" : mode is null ? "Modo no disponible con el plan actual" : mode == PerformanceMode ? "Ya activo" : "Modo anterior respaldado antes de aplicar"));
        var powerAvailable = ac && mode is null && scheme is not null;
        options.Add(new("power", "Plan Alto rendimiento existente", "Alternativa al modo moderno. Solo usa un plan ya instalado y guarda el plan anterior. Puede aumentar consumo y temperatura.", "Energía",
            powerAvailable, powerAvailable && scheme?.Active != true, scheme?.Active == true,
            !ac ? "Conecta el equipo a corriente" : mode is not null ? "Se ofrece el modo moderno de energía" : scheme is null ? "Plan no instalado; se conservará el actual" : scheme.Active ? "Ya activo" : "Disponible"));
        var visual = platform?.CanReduceVisualEffects() == true;
        options.Add(new("visual-effects", "Reducir efectos visuales de Windows", "Reduce transiciones de menús, listas y ayudas. Puede cambiar la experiencia de uso; no cambia los gráficos de los juegos.", "Respuesta del escritorio",
            visual, false, false, visual ? "Opcional · cada preferencia queda respaldada" : "Windows no permite consultar todos los efectos"));
        foreach (var entry in overview.StartupEntries.Where(PcStartupSelection.CanOffer).Take(100))
        {
            var id = "startup:" + entry.Id;
            reviewedStartup[id] = entry;
            options.Add(new(id, "Quitar esta entrada de inicio: " + entry.Name,
                "Retira esta entrada del inicio de tu usuario para la próxima sesión. Windows puede tenerla deshabilitada en Configuración y la app puede tener otras reglas de inicio; no se confirma actividad ni ahorro de recursos por su sola presencia. Podrás abrirla manualmente. No cierra procesos actuales. Selecciona solo apps que no necesites al iniciar; sus notificaciones y sincronización pueden esperar hasta abrirla. Comando revisado: " + entry.Command,
                "Inicio de sesión · elección manual", entry.CanChange, false, false,
                entry.CanChange ? "Entrada registrada · actividad al iniciar sin verificar · respaldo exacto" : "Windows no permite modificar esta entrada con los permisos actuales"));
        }
        reviewed = new(options);
        return reviewed;
    }

    public async Task<PcOptimizationResult> ApplyAsync(IReadOnlyCollection<string> ids, IProgress<PcOptimizationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var approved = reviewed;
        var approvedStartup = reviewedStartup;
        var approvedTarget = applicationTarget;
        var approvedGeneration = targetGeneration;
        if (approved is null || ids.Count == 0 || ids.Count > approved.Options.Count || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count || ids.Any(id => !approved.Options.Any(option => option.Id == id)))
            return new([new("selection", "Selección del PC", false, false, false, "Prepara una selección válida antes de aplicar. No se cambió Windows.")], false);
        var selected = approved.Options.Where(option => ids.Contains(option.Id)).ToList();
        var results = new List<PcOptimizationStep>();
        var cancelled = false;
        if (cancellationToken.IsCancellationRequested) return new([], true);
        PcOptimizationPreview live;
        try { live = await PreviewAsync(cancellationToken); }
        catch (OperationCanceledException) { return new([], true); }
        if (targetGeneration != approvedGeneration)
            return new(selected.Select(option => new PcOptimizationStep(option.Id, option.Title, false, false, true, "El producto elegido cambió. Prepara de nuevo la selección; no se aplicó ningún paso.")).ToList(), false);
        foreach (var option in selected)
        {
            if (cancellationToken.IsCancellationRequested) { cancelled = true; break; }
            progress?.Report(new(results.Count, selected.Count, option.Title));
            try
            {
                var current = live.Options.FirstOrDefault(item => item.Id == option.Id);
                if (!option.Available || current?.Available != true)
                    results.Add(new(option.Id, option.Title, false, false, true, current?.Status ?? "El ajuste ya no está disponible."));
                else if (approvedStartup.TryGetValue(option.Id, out var startup) &&
                    (!reviewedStartup.TryGetValue(option.Id, out var liveStartup) || !string.Equals(startup.Command, liveStartup.Command, StringComparison.Ordinal)))
                    results.Add(new(option.Id, option.Title, false, false, true, "La entrada de inicio cambió desde la revisión. Se conservó; prepara de nuevo la selección."));
                else if (current.AlreadyApplied)
                    results.Add(new(option.Id, option.Title, true, false, false, "Ya estaba aplicado; se conservó el estado actual."));
                else
                {
                    var outcome = approvedStartup.TryGetValue(option.Id, out var chosenStartup)
                        ? await native.DisableStartupCheckedAsync(chosenStartup.Id, chosenStartup.Command, cancellationToken)
                        : option.Id switch
                    {
                        "application-gpu" => approvedTarget is { } target ? await native.SetApplicationGpuPreferenceAsync(target, cancellationToken) : new(false, "Vuelve a elegir el ejecutable."),
                        "application-timer" => approvedTarget is { } timerTarget ? await timers.SetEnabledAsync(timerTarget, true, cancellationToken) : new(false, "Vuelve a elegir el ejecutable."),
                        "power-mode" => (native as NativeOptimizationService ?? (native as SystemActionOptimizationService)?.Local) is { } mode ? await mode.SetBestPerformanceModeAsync(() => readAc() == true, cancellationToken) : new(false, "Modo no disponible."),
                        "power" => readAc() != true ? new(false, "Se desconectó la corriente; se conservó el plan.") :
                            (native as NativeOptimizationService ?? (native as SystemActionOptimizationService)?.Local) is { } power ? await power.SetPowerSchemeCheckedAsync(FortniteProfileService.HighPerformanceScheme, () => readAc() == true, cancellationToken) :
                            await native.SetPowerSchemeAsync(FortniteProfileService.HighPerformanceScheme, cancellationToken),
                        "visual-effects" => (native as NativeOptimizationService ?? (native as SystemActionOptimizationService)?.Local) is { } effects ? await effects.ReduceVisualEffectsAsync(cancellationToken) : new(false, "Efectos no disponibles."),
                        _ => await native.SetToggleAsync(option.Id, option.Id is not ("xbox-captures" or "animations"), cancellationToken)
                    };
                    results.Add(new(option.Id, option.Title, outcome.Success, outcome.Changed, false, outcome.Message, outcome.Verification));
                }
            }
            catch (OperationCanceledException)
            {
                results.Add(new(option.Id, option.Title, false, false, false, "Paso interrumpido. Verificación incierta: revisa los respaldos antes de reintentar.", ChangeVerification.Uncertain));
                cancelled = true; break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or Win32Exception or SecurityException)
            { results.Add(new(option.Id, option.Title, false, false, false, ex.Message + " Verificación incierta: revisa los respaldos antes de reintentar.", ChangeVerification.Uncertain)); }
            progress?.Report(new(results.Count, selected.Count, option.Title));
        }
        if (cancelled)
            foreach (var option in selected.Skip(results.Count)) results.Add(new(option.Id, option.Title, false, false, true, "Cancelado antes de iniciar este paso."));
        return new(results, cancelled);
    }
}
