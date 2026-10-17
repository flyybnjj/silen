using System.Collections.ObjectModel;
using System.Windows.Input;
using Silen.Windows;
using Silen.Core;

namespace Silen.App;


public sealed partial class ToolsViewModel : ObservableObject
{
    private readonly INativeOptimizationService service;
    private readonly Func<string, Func<CancellationToken, Task>, Task<bool>> run;
    private readonly Func<bool> canRun;
    private readonly Func<bool> canApply;
    private string status = "Lee los ajustes actuales para elegir una mejora. No hace falta analizar primero.";
    private string cleanupStatus = "Calculando temporales de al menos 24 horas. Se conservarán para recuperar antes de una purga definitiva.";
    private bool inspected;
    private bool previewed;
    private bool actionError;
    private PowerScheme? selectedPowerScheme;
    private StartupChoice? selectedStartup;
    private AppliedOptimization? selectedChange;
    private long directTempBytes;
    private bool directTempReady;
    private bool directTempFailed;
    private readonly Func<string?> chooseGameExecutable;
    private string? gameExecutable;
    private string gpuStatus = "Elige el ejecutable. Windows decidirá la GPU efectiva; vuelve a abrir el juego después de aplicar.";

    internal ToolsViewModel(INativeOptimizationService service,
        Func<string, Func<CancellationToken, Task>, Task<bool>> run, Func<bool> canRun, Func<string?>? chooseGameExecutable = null,
        Func<string, string, bool>? confirmAction = null, Func<bool>? canApply = null)
    {
        this.service = service; this.run = run; this.canRun = canRun;
        this.canApply = canApply ?? canRun;
        this.chooseGameExecutable = chooseGameExecutable ?? (() => null);
        InitializeManual(confirmAction ?? ((_, _) => false));
        InitializeRecovery();
        ChooseGameCommand = Command(_ => ChooseGameAsync());
        ApplyGpuCommand = Command(_ => ApplyGpuAsync(), _ => CanApplySelection() && gameExecutable is not null);
        RefreshCommand = Command(_ => RefreshAsync());
        EnableToggleCommand = Command(p => SetToggleAsync(p, true), p => p is ToggleChoice t && t.Setting.CanEnable && t.Setting.Enabled != true);
        DisableToggleCommand = Command(p => SetToggleAsync(p, false), p => p is ToggleChoice t && t.Setting.CanDisable && (t.Setting.UsesBackupRestore || t.Setting.Enabled != false));
        ApplyPowerCommand = Command(_ => ApplyPowerAsync(), _ => SelectedPowerScheme is { Active: false });
        ToggleStartupCommand = Command(_ => ToggleStartupAsync(), _ => SelectedStartup?.Entry.CanChange == true);
        UndoCommand = Command(_ => UndoAsync(), _ => SelectedChange is { CanUndo: true });
        PreviewTempCommand = Command(_ => PreviewTempAsync());
        CleanTempCommand = Command(_ => CleanTempAsync(), _ => CanApplySelection() && TemporaryFiles.Any(f => f.Selected));
        CleanAllTempCommand = Command(_ => CleanAllTempAsync(), _ => CanApplySelection() && directTempReady && directTempBytes > 0);
        SelectAllTempCommand = new RelayCommand(_ => SelectFiles(true), _ => canRun() && TemporaryFiles.Count > 0);
        ClearTempSelectionCommand = new RelayCommand(_ => SelectFiles(false), _ => canRun() && TemporaryFiles.Any(f => f.Selected));
    }

    private bool CanApplySelection() => canRun() && canApply();

    private ICommand Command(Func<object?, Task> work, Func<object?, bool>? extra = null) =>
        new AsyncCommand(work, p => canRun() && (extra?.Invoke(p) ?? true), ex => Status = ex.Message);

    public ICommand RefreshCommand { get; }
    internal event Action<WindowsUpdateHealth>? WindowsUpdateStateChanged;
    public ICommand EnableToggleCommand { get; }
    public ICommand DisableToggleCommand { get; }
    public ICommand ApplyPowerCommand { get; }
    public ICommand ToggleStartupCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand PreviewTempCommand { get; }
    public ICommand CleanTempCommand { get; }
    public ICommand CleanAllTempCommand { get; }
    public ICommand ChooseGameCommand { get; }
    public ICommand ApplyGpuCommand { get; }
    public string GameExecutableName => gameExecutable is null ? "Ningún juego elegido" : System.IO.Path.GetFileName(gameExecutable);
    public string GpuStatus { get => gpuStatus; private set => Set(ref gpuStatus, value); }
    public string DirectTempLabel => directTempReady ? $"Limpiar archivos temporales ({Size(directTempBytes)})" : directTempFailed ? "Limpiar archivos temporales (tamaño no disponible)" : "Limpiar archivos temporales (calculando…)";
    public ICommand SelectAllTempCommand { get; }
    public ICommand ClearTempSelectionCommand { get; }
    public ObservableCollection<ToggleChoice> ImprovementToggles { get; } = [];
    public ObservableCollection<ToggleChoice> GamingToggles { get; } = [];
    public ObservableCollection<OptimizationCategory> OptimizationCategories { get; } = [];
    public ObservableCollection<PowerScheme> PowerSchemes { get; } = [];
    public ObservableCollection<StartupChoice> StartupEntries { get; } = [];
    public ObservableCollection<AppliedOptimization> Changes { get; } = [];
    public ObservableCollection<TemporaryFileChoice> TemporaryFiles { get; } = [];
    public string Status { get => status; private set => Set(ref status, value); }
    public string CleanupStatus { get => cleanupStatus; private set => Set(ref cleanupStatus, value); }
    public bool HasInspection => inspected;
    public bool HasStartup => StartupEntries.Count > 0;
    public bool HasChanges => Changes.Count > 0;
    public bool HasPowerSchemes => PowerSchemes.Count > 0;
    public bool HasTemporaryFiles => TemporaryFiles.Count > 0;
    public bool HasPreview => previewed;
    public bool HasActionError => actionError;
    public string PowerStatus => PowerSchemes.FirstOrDefault(p => p.Active) is { } active ? $"Plan actual: {active.Name}" : "No se pudo identificar el plan activo. Actualiza los ajustes.";
    public string StartupActionLabel => SelectedStartup?.Entry.Enabled == false ? "Añadir restauración de inicio" : "Añadir retirada del inicio";
    public string TempSelectionSummary => $"{SpanishText.Count(TemporaryFiles.Count(f => f.Selected), "archivo seleccionado", "archivos seleccionados")} de {TemporaryFiles.Count} · {Size(TemporaryFiles.Where(f => f.Selected).Sum(f => f.File.Bytes))}";
    public PowerScheme? SelectedPowerScheme { get => selectedPowerScheme; set { Set(ref selectedPowerScheme, value); CommandManager.InvalidateRequerySuggested(); } }
    public StartupChoice? SelectedStartup { get => selectedStartup; set { if (Set(ref selectedStartup, value)) Changed(nameof(StartupActionLabel)); CommandManager.InvalidateRequerySuggested(); } }
    public AppliedOptimization? SelectedChange { get => selectedChange; set { Set(ref selectedChange, value); CommandManager.InvalidateRequerySuggested(); } }

    public Task<bool> RefreshAsync() => run("Leyendo ajustes actuales…", async token =>
    {
        await RefreshStateAsync(token);
        await RefreshTempTotalAsync(token);
        await RefreshRecoveryAsync(token);
        actionError = false; Changed(nameof(HasActionError));
        Status = "Ajustes actuales leídos. Añade los cambios y confirma Aplicar selección manual.";
    });

    internal async Task RefreshStateAsync(CancellationToken token)
    {
        var overview = await service.InspectAsync(token);
        token.ThrowIfCancellationRequested();
        var powerId = SelectedPowerScheme?.Id;
        var startupId = SelectedStartup?.Entry.Id;
        var changeId = SelectedChange?.Id;
        ImprovementToggles.Clear(); GamingToggles.Clear();
        foreach (var toggle in overview.Toggles)
        {
            var row = new ToggleChoice(toggle);
            if (toggle.Id == "animations") ImprovementToggles.Add(row);
            else if (toggle.Id is "game-mode" or "xbox-captures" or "gamebar-background" or "gaming-network-bandwidth") GamingToggles.Add(row);
        }
        OptimizationCategories.Clear();
        foreach (var group in overview.Toggles.Where(toggle => toggle.Id is not ("game-mode" or "xbox-captures" or "gamebar-background" or "gaming-network-bandwidth")).GroupBy(toggle => toggle.Category))
            OptimizationCategories.Add(new(group.Key, group.Select(toggle => new ToggleChoice(toggle)).ToList()));
        PowerSchemes.Clear(); foreach (var scheme in overview.PowerSchemes) PowerSchemes.Add(scheme);
        StartupEntries.Clear(); foreach (var entry in overview.StartupEntries) StartupEntries.Add(new(entry));
        Changes.Clear(); foreach (var change in overview.Changes.OrderByDescending(c => c.AppliedAt)) Changes.Add(change with { Title = change.Title.Replace("WinUtil · ", "", StringComparison.Ordinal) });
        SelectedPowerScheme = PowerSchemes.FirstOrDefault(p => p.Id == powerId) ?? PowerSchemes.FirstOrDefault(p => p.Active);
        SelectedStartup = StartupEntries.FirstOrDefault(p => p.Entry.Id == startupId);
        SelectedChange = Changes.FirstOrDefault(c => c.Id == changeId);
        inspected = true; Changed(null);
    }

    private Task<bool> ApplyAsync(string title, Func<CancellationToken, Task<OptimizationOutcome>> action) => run(title, async token =>
    {
        try
        {
            var outcome = await action(token);
            Status = outcome.Message;
            actionError = !outcome.Success; Changed(nameof(HasActionError));
        }
        catch (OperationCanceledException)
        {
            Status = "Operación cancelada. Revisa el estado actual y los cambios guardados antes de volver a aplicar.";
            actionError = true; Changed(nameof(HasActionError));
            throw;
        }
        catch (Exception ex)
        {
            Status = ex.Message; actionError = true; Changed(nameof(HasActionError));
            throw;
        }
        finally
        {


            try { await RefreshStateAsync(CancellationToken.None); }
            catch (Exception ex)
            {
                Status += $" No se pudo actualizar el estado: {ex.Message}. Pulsa Actualizar ajustes para volver a leerlo.";
                actionError = true; Changed(nameof(HasActionError));
            }
            try { WindowsUpdateStateChanged?.Invoke(await service.InspectWindowsUpdateAsync(CancellationToken.None)); }
            catch (Exception ex) { Status += " No se pudo actualizar Windows Update: " + ex.Message; }
        }
    });

    private Task SetToggleAsync(object? parameter, bool enabled) => parameter is ToggleChoice row
        ? QueueManual(new(row.Setting.Id, row.Setting.Name, row.Setting.Description, ManualChangeKind.Toggle, enabled))
        : Task.CompletedTask;

    private Task ApplyPowerAsync() => SelectedPowerScheme is { } scheme
        ? QueueManual(new("power", "Plan de energía: " + scheme.Name, "Puede aumentar consumo y temperatura. Se guardará el plan anterior.", ManualChangeKind.Power, Target: scheme.Id)) : Task.CompletedTask;

    private Task ToggleStartupAsync() => SelectedStartup is { } row
        ? QueueManual(new("startup:" + row.Entry.Id, "Inicio: " + row.Entry.Name, "Cambia esta entrada Run para próximas sesiones; no cierra procesos.", ManualChangeKind.Startup, !row.Entry.Enabled, row.Entry.Id, row.Entry.Command)) : Task.CompletedTask;

    private Task UndoAsync() => SelectedChange is { CanUndo: true } change
        && confirmManual("Restaurar cambio", "¿Restaurar «" + change.Title + "»? Se comprobarán conflictos antes de escribir.")
        ? ApplyAsync("Restaurando el estado anterior…", token => service.UndoAsync(change.Id, token)) : Task.CompletedTask;

    private Task PreviewTempAsync() => run("Preparando vista previa de temporales…", async token =>
    {
        TemporaryFiles.Clear();
        previewed = false; CleanupStatus = "Preparando una nueva vista previa. Las selecciones anteriores se han descartado."; Changed(null);
        try
        {
            var preview = await service.PreviewTempAsync(token);
            token.ThrowIfCancellationRequested();
            foreach (var file in preview.Files)
            {
                var row = new TemporaryFileChoice(file);
                row.PropertyChanged += (_, _) => { Changed(nameof(TempSelectionSummary)); CommandManager.InvalidateRequerySuggested(); };
                TemporaryFiles.Add(row);
            }
            previewed = true;
            CleanupStatus = $"{preview.Message} Total calculado: {Size(preview.TotalBytes)}.{(preview.Truncated ? " Se alcanzó el límite; vuelve a previsualizar después de limpiar." : "")}";
            Changed(null);
        }
        catch (OperationCanceledException) { CleanupStatus = "Vista previa cancelada. Pulsa Ver archivos para limpiar y vuelve a seleccionar los archivos."; throw; }
        catch (Exception ex) { CleanupStatus = $"No se pudo preparar la vista previa: {ex.Message}. Vuelve a previsualizar antes de limpiar."; throw; }
    });

    private static string CleanupResultLabel(TemporaryCleanupResult result) =>
        $"{result.Message} {SpanishText.Count(result.Removed, "archivo retirado", "archivos retirados")} · {SpanishText.Count(result.Skipped, "omitido", "omitidos")}. En recuperación: {Size(result.RecoverableBytes)}. Espacio liberado: {Size(result.BytesFreed)}.";

    private Task CleanTempAsync()
    {
        var ids = TemporaryFiles.Where(f => f.Selected).Select(f => f.File.Id).ToArray();
        if (ids.Length == 0) return Task.CompletedTask;
        if (!confirmManual("Retirar temporales seleccionados", $"{SpanishText.Form(ids.Length, "Se conservará", "Se conservarán")} {SpanishText.Count(ids.Length, "archivo", "archivos")} en recuperación. El espacio se libera únicamente con una purga definitiva. ¿Continuar?")) return Task.CompletedTask;
        return run("Limpiando los temporales seleccionados…", async token =>
        {
            var result = await CleanWithProgressAsync(ids, token);
            CleanupStatus = CleanupResultLabel(result);
            TemporaryFiles.Clear(); previewed = false; Changed(null);
        });
    }

    private void SelectFiles(bool selected) { foreach (var file in TemporaryFiles) file.Selected = selected; }
    private Task ChooseGameAsync()
    {
        var path = chooseGameExecutable();
        if (path is not null) { gameExecutable = path; GpuStatus = "Se guardará una preferencia de GPU para este ejecutable. Puede aumentar el consumo; no cambia sus archivos."; Changed(nameof(GameExecutableName)); CommandManager.InvalidateRequerySuggested(); }
        return Task.CompletedTask;
    }
    private Task ApplyGpuAsync() => gameExecutable is { } path ? ApplyAsync("Guardando preferencia gráfica del juego…", async token =>
    {
        var result = await service.SetApplicationGpuPreferenceAsync(path, token); GpuStatus = result.Message; return result;
    }) : Task.CompletedTask;
    private async Task RefreshTempTotalAsync(CancellationToken token)
    {
        directTempReady = false; directTempFailed = false; Changed(nameof(DirectTempLabel));
        try
        {
            var result = await service.PreviewTempAsync(token);
            directTempBytes = result.TotalBytes; directTempReady = true;
            CleanupStatus = result.Truncated ? "Tamaño de una tanda de temporales elegibles. Se alcanzó el límite; vuelve a limpiar para revisar los restantes." : "Solo temporales de al menos 24 horas. Se omiten archivos cambiados, enlaces o en uso. Se conservarán para recuperar; el espacio se libera al purgar.";
        }
        catch (OperationCanceledException) { directTempFailed = true; CleanupStatus = "Cálculo de temporales cancelado. Actualiza los ajustes para volver a intentarlo."; throw; }
        catch (Exception ex) { directTempFailed = true; CleanupStatus = "No se pudo calcular la limpieza: " + ex.Message; }
        finally { Changed(nameof(DirectTempLabel)); CommandManager.InvalidateRequerySuggested(); }
    }

    private Task<bool> CleanAllTempAsync() => run("Limpiando temporales del usuario…", async token =>
    {
        try
        {
            var fresh = await service.PreviewTempAsync(token);
            if (fresh.Files.Count == 0) { CleanupStatus = "No hay temporales elegibles en esta tanda."; ActivityResult?.Invoke("No hay temporales para retirar.", "Solo revisé los temporales elegibles de tu usuario."); return; }
            if (!confirmManual("Limpiar temporales con recuperación", $"{SpanishText.Form(fresh.Files.Count, "Se retirará", "Se retirarán")} {SpanishText.Count(fresh.Files.Count, "archivo", "archivos")} ({Size(fresh.TotalBytes)}) de la carpeta temporal del usuario. El lote quedará disponible para restaurarlo o purgarlo. No se libera espacio hasta confirmar una purga definitiva. ¿Continuar?")) { ActivityResult?.Invoke("He cancelado la limpieza.", "No he retirado archivos. Puedes revisar las opciones cuando quieras."); return; }
            var result = await CleanWithProgressAsync(fresh.Files.Select(file => file.Id).ToArray(), token);
            CleanupStatus = CleanupResultLabel(result);
            TemporaryFiles.Clear(); previewed = false;
        }
        catch (OperationCanceledException) { CleanupStatus = "Limpieza cancelada antes de completarse. No se iniciarán más eliminaciones."; throw; }
        catch (Exception ex) { CleanupStatus = "No se pudo completar la limpieza: " + ex.Message; throw; }
        finally
        {
            var resultMessage = CleanupStatus;
            await RefreshTempTotalAsync(CancellationToken.None);
            await RefreshRecoveryAsync(CancellationToken.None);
            CleanupStatus = resultMessage; Changed(null);
        }
    });
    private static string Size(long bytes) => bytes >= 1073741824 ? $"{(bytes / 1073741824d).ToString("N1", Labels.Culture)} GiB" : bytes >= 1048576 ? $"{(bytes / 1048576d).ToString("N1", Labels.Culture)} MiB" : bytes >= 1024 ? $"{(bytes / 1024d).ToString("N1", Labels.Culture)} KiB" : $"{bytes.ToString("N0", Labels.Culture)} B";
}

public sealed record ToggleChoice(ToggleSetting Setting)
{
    public string EnableLabel => "Añadir ajuste";
    public string DisableLabel => Setting.UsesBackupRestore ? "Añadir restauración" : "Añadir desactivación";
    public string State => Setting.Enabled switch { true => Setting.UsesBackupRestore ? "Aplicado" : "Activado", false => Setting.UsesBackupRestore ? "Sin aplicar" : "Desactivado", _ => "Predeterminado de Windows / sin dato" };
}

public sealed record OptimizationCategory(string Name, IReadOnlyList<ToggleChoice> Toggles);

public sealed record StartupChoice(Silen.Windows.StartupEntry Entry)
{
    public string State => Entry.Enabled ? "Presente en Run" : "Retirada de Run";
}

public sealed class TemporaryFileChoice(TemporaryFileItem file) : ObservableObject
{
    private bool selected;
    public TemporaryFileItem File { get; } = file;
    public bool Selected { get => selected; set => Set(ref selected, value); }
    public string SizeLabel => File.Bytes >= 1048576 ? $"{(File.Bytes / 1048576d).ToString("N1", Labels.Culture)} MiB" : File.Bytes >= 1024 ? $"{(File.Bytes / 1024d).ToString("N1", Labels.Culture)} KiB" : $"{File.Bytes.ToString("N0", Labels.Culture)} B";
}
