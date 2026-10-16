using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Silen.Windows;


public sealed class FortniteProfileService : IFortniteProfileService
{
    public const string HighPerformanceScheme = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private readonly INativeOptimizationService native;
    private readonly IFortniteGameService game;
    private readonly Func<bool?> onAcPower;
    private readonly Func<string, CancellationToken, Task<OptimizationOutcome>> setGpu;
    private readonly Func<string, bool?> readGpu;
    private readonly Func<CancellationToken, Task<OptimizationOutcome>> applyWinUtil;
    private FortniteProfilePreview? preview;

    public FortniteProfileService() : this(new NativeOptimizationService(), new FortniteGameService()) { }
    private FortniteProfileService(NativeOptimizationService native, IFortniteGameService game) :
        this(native, game, ReadAcPower, native.SetGameGpuPreferenceAsync, native.ReadGameGpuPreference, native.ApplyWinUtilGameModeAsync) { }
    internal FortniteProfileService(INativeOptimizationService native, IFortniteGameService game, Func<bool?> onAcPower,
        Func<string, CancellationToken, Task<OptimizationOutcome>> setGpu, Func<string, bool?> readGpu,
        Func<CancellationToken, Task<OptimizationOutcome>> applyWinUtil)
    { this.native = native; this.game = game; this.onAcPower = onAcPower; this.setGpu = setGpu; this.readGpu = readGpu; this.applyWinUtil = applyWinUtil; }

    public async Task<FortniteProfilePreview> PreviewAsync(string? installDirectory = null, CancellationToken cancellationToken = default)
    {
        preview = null;
        var installation = await game.DetectAsync(installDirectory, cancellationToken);
        var windows = await native.InspectAsync(cancellationToken);
        var gameChanges = await game.ReadChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var ac = onAcPower() == true;
        var powerMode = native is NativeOptimizationService modeService && ac ? modeService.ReadPowerMode() : null;
        var visualEffects = native is NativeOptimizationService effectsService && effectsService.CanReduceVisualEffects();
        var power = windows.PowerSchemes.FirstOrDefault(x => x.Id.Equals(HighPerformanceScheme, StringComparison.OrdinalIgnoreCase));
        var gameMode = windows.Toggles.FirstOrDefault(x => x.Id == "game-mode");
        var captures = windows.Toggles.FirstOrDefault(x => x.Id == "xbox-captures");
        var animations = windows.Toggles.FirstOrDefault(x => x.Id == "animations");
        var gpuAvailable = installation.Installed && installation.ExecutablePath is not null && ac;
        var gpu = gpuAvailable ? readGpu(installation.ExecutablePath!) : null;
        var configAvailable = installation.Installed && installation.SettingsPath is not null && File.Exists(installation.SettingsPath) && !installation.IsRunning;
        var options = new List<FortniteProfileOption>
        {
            new("winutil-game-mode", "Priorizar el juego", "Activa las preferencias de Modo Juego de Windows y guarda sus originales.", true, true, gameMode?.Enabled == true ? "Ya activado" : "Activar Modo Juego"),
            new("xbox-captures", "Desactivar capturas Xbox", "Desactiva las preferencias de grabación Xbox/Game Bar del usuario. Si grabas partidas, desmarca este paso.", true, true, captures?.Enabled == false ? "Ya desactivadas" : "Desactivar capturas en segundo plano"),
            new("animations", "Reducir animaciones de Windows", "Paso opcional para reducir transiciones de ventanas; también reduce el movimiento de Silen.", animations?.Enabled is not null, false, animations?.Enabled == false ? "Ya reducidas" : "Opcional; no aumenta los FPS del juego por sí solo"),
            new("power", "Plan Alto rendimiento existente", "Usa el plan estándar Alto rendimiento con corriente cuando el modo moderno no esté disponible. Puede aumentar consumo y temperatura.", ac && power is not null && powerMode is null, ac && power is not null && powerMode is null, powerMode is not null ? "Se usará el modo moderno de energía" : !ac ? "Conecta el equipo a corriente" : power is null ? "Ese plan no existe; se conservará el actual" : power.Active ? "Ya activo" : "Plan anterior guardado"),
            new("gpu-preference", "Preferir GPU de alto rendimiento para Fortnite", "Guarda una preferencia gráfica para el ejecutable detectado. Windows decide la GPU efectiva; vuelve a abrir el juego.", gpuAvailable, gpuAvailable, !installation.Installed ? "Primero detecta Fortnite" : !ac ? "Disponible con alimentación de corriente" : gpu == true ? "Ya preferida" : "Preferencia por aplicación; requiere reiniciar el juego"),
            new("fortnite-settings", "Calidad competitiva baja de Fortnite", "Reduce las claves de calidad existentes, desactiva VSync y motion blur y muestra FPS. Conserva resolución, límite FPS, renderizador y controles.", configAvailable, configAvailable, !installation.Installed ? "Primero detecta Fortnite" : installation.IsRunning ? "Cierra Fortnite antes de editar" : !configAvailable ? installation.Message : "Cambiar calidad visual; respaldo exacto disponible")
        };
        options.Add(new("power-mode", "Máximo rendimiento con corriente", "Activa el modo Mejor rendimiento de Windows sin crear planes extra. Puede aumentar consumo, ruido y temperatura; el modo anterior queda guardado.", powerMode is not null, powerMode is not null, powerMode is null ? "Requiere corriente y un plan Equilibrado compatible" : powerMode == Guid.Parse("ded574b5-45a0-4f42-8737-46345c09c238") ? "Ya activo" : "Modo anterior guardado"));
        options.Add(new("visual-effects", "Reducir efectos visuales de Windows", "Reduce animaciones de menús, listas y ayudas mediante Windows. Cambia la respuesta del escritorio; no modifica los gráficos del juego.", visualEffects, visualEffects, visualEffects ? "Aplicación inmediata; conserva cada preferencia original" : "Windows no permitió leer todos los efectos"));
        foreach (var group in PerformanceTweaks.ProfileGroups)
        {
            var available = native is NativeOptimizationService preferences && preferences.CanApplyPerformanceTweak(group.Id);
            options.Add(new(group.Id, group.Title, group.Description, available, available, available
                ? "Preferencia reversible; algunas requieren reiniciar la app o sesión"
                : "Windows no permite modificar este ajuste con los permisos actuales. Puede estar restringido por una política."));
        }
        preview = new(installation, options, windows.Changes.Concat(gameChanges).OrderByDescending(x => x.AppliedAt).ToList(),
            "Optimiza automáticamente o personaliza los ajustes disponibles. Los originales quedan guardados para restaurar.");
        return preview;
    }

    public async Task<FortniteProfileResult> ApplyAsync(IReadOnlyCollection<string> optionIds,
        IProgress<FortniteProfileProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var current = preview;
        if (current is null || optionIds.Count == 0 || optionIds.Count > current.Options.Count || optionIds.Distinct().Count() != optionIds.Count ||
            optionIds.Any(id => !current.Options.Any(option => option.Id == id)))
            return new([new("profile", "Perfil de Fortnite", false, false, "Detecta y revisa un perfil válido antes de optimizar. No se aplicaron cambios.")], false, "No se completaron cambios.");
        var results = new List<FortniteProfileStepResult>();
        var steps = current.Options.Where(x => optionIds.Contains(x.Id)).ToList();
        var cancelled = false;
        foreach (var step in steps)
        {
            if (cancellationToken.IsCancellationRequested) { cancelled = true; break; }
            progress?.Report(new(results.Count, steps.Count, step.Title));
            try
            {
                var outcome = !step.Available ? new OptimizationOutcome(false, step.Status) : step.Id switch
                {
                    "winutil-game-mode" => await applyWinUtil(cancellationToken),
                    "xbox-captures" => await native.SetToggleAsync("xbox-captures", false, cancellationToken),
                    "animations" => await native.SetToggleAsync("animations", false, cancellationToken),
                    "power" => onAcPower() != true ? new(false, "La alimentación cambió o no se confirmó corriente. Se conservó el plan actual.") :
                        native is NativeOptimizationService nativePower ? await nativePower.SetPowerSchemeCheckedAsync(HighPerformanceScheme, () => onAcPower() == true, cancellationToken) :
                        await native.SetPowerSchemeAsync(HighPerformanceScheme, cancellationToken),
                    "gpu-preference" => onAcPower() != true ? new(false, "La alimentación cambió. Se conservó la preferencia GPU.") :
                        native is NativeOptimizationService nativeGpu ? await nativeGpu.SetGameGpuPreferenceCheckedAsync(current.Installation.ExecutablePath!, () => onAcPower() == true, cancellationToken) :
                        await setGpu(current.Installation.ExecutablePath!, cancellationToken),
                    "fortnite-settings" => await game.ApplyCompetitiveSettingsAsync(cancellationToken),
                    "power-mode" => native is NativeOptimizationService modes ? await modes.SetBestPerformanceModeAsync(() => onAcPower() == true, cancellationToken) : new(false, "Modo no disponible."),
                    "visual-effects" => native is NativeOptimizationService effects ? await effects.ReduceVisualEffectsAsync(cancellationToken) : new(false, "Efectos no disponibles."),
                    _ => native is NativeOptimizationService preferences ? await preferences.ApplyPerformanceTweakAsync(step.Id, cancellationToken) : throw new InvalidOperationException("Paso no disponible.")
                };
                results.Add(new(step.Id, step.Title, outcome.Success, outcome.Changed, outcome.Message));
            }
            catch (OperationCanceledException)
            { results.Add(new(step.Id, step.Title, false, false, "Paso interrumpido. Revisa los respaldos; puede haber cambios parciales recuperables.")); cancelled = true; break; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or Win32Exception)
            { results.Add(new(step.Id, step.Title, false, false, ex.Message)); }
            progress?.Report(new(results.Count, steps.Count, step.Title));
        }
        var changed = results.Count(x => x.Changed);
        var message = cancelled ? $"Optimización cancelada. {changed} pasos con cambios confirmados; revisa los respaldos disponibles." :
            results.Any(x => !x.Success) ? $"No se completaron todos los pasos. {changed} con cambios confirmados; revisa cada resultado." :
            $"Perfil aplicado y comprobado. {changed} pasos con cambios; los demás ya tenían el estado elegido. Vuelve a abrir Fortnite.";
        return new(results, cancelled, message);
    }

    public Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default) =>
        changeId.StartsWith("fortnite:", StringComparison.Ordinal) ? game.UndoAsync(changeId, cancellationToken) : native.UndoAsync(changeId, cancellationToken);

    private static bool? ReadAcPower() => GetSystemPowerStatus(out var status) ? status.AcLineStatus switch { 1 => true, 0 => false, _ => null } : null;
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus { public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
