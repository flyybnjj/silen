using System.Collections.ObjectModel;
using System.Windows.Input;
using Silen.Windows;

namespace Silen.App;


public sealed class FortniteViewModel : ObservableObject
{
    private readonly IFortniteProfileService service;
    private readonly Func<string, Func<CancellationToken, Task>, Task<bool>> run;
    private readonly Func<bool> canRun;
    private readonly Func<CancellationToken, Task> refreshTools;
    private readonly Func<string?> chooseFolder;
    private readonly Func<bool> confirmOptimization;
    private FortniteProfilePreview? preview;
    private string? manualDirectory;
    private string status = "";
    private bool optimizing;
    private string progressTitle = "";
    private int completedSteps;
    private int totalSteps;
    private bool actionError;
    private bool applying;
    private int progressGeneration;
    private AppliedOptimization? selectedChange;

    internal FortniteViewModel(IFortniteProfileService service,
        Func<string, Func<CancellationToken, Task>, Task<bool>> run, Func<bool> canRun,
        Func<CancellationToken, Task> refreshTools, Func<string?> chooseFolder, Func<bool> confirmOptimization)
    {
        this.service = service; this.run = run; this.canRun = canRun;
        this.refreshTools = refreshTools; this.chooseFolder = chooseFolder;
        this.confirmOptimization = confirmOptimization;
        OptimizeCommand = Command(_ => OptimizeAsync());
        DetectCommand = Command(_ => DetectAsync());
        ChooseFolderCommand = Command(_ => ChooseFolderAsync());
        ApplyCommand = Command(_ => ApplyAsync(), _ => HasPreview && Options.Any(o => o.Option.Available && o.Selected));
        ApplySelectionCommand = Command(_ => confirmOptimization() ? ApplyAsync() : Task.CompletedTask, _ => HasPreview && Options.Any(o => o.Option.Available && o.Selected));
        ApplyGameProfileCommand = Command(_ => confirmOptimization() ? run("Aplicando perfil opcional de Fortnite…", token => ApplySelectedAsync(token, false, true)) : Task.FromResult(false),
            _ => IsInstalled && !IsGameRunning && GameOptions.Any(option => option.Option.Available && option.Selected));
        UndoCommand = Command(_ => UndoAsync(), _ => SelectedChange is { CanUndo: true });
    }

    private ICommand Command(Func<object?, Task> work, Func<object?, bool>? extra = null) =>
        new AsyncCommand(work, p => canRun() && (extra?.Invoke(p) ?? true), ex => { Status = ex.Message; SetError(true); });

    public ICommand DetectCommand { get; }
    public ICommand OptimizeCommand { get; }
    public ICommand ChooseFolderCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand ApplySelectionCommand { get; }
    public ICommand ApplyGameProfileCommand { get; }
    public IEnumerable<FortniteOptionChoice> GameOptions => Options.Where(option => option.Option.Id is "fortnite-settings" or "gpu-preference");
    public ICommand UndoCommand { get; }
    public ObservableCollection<FortniteOptionChoice> Options { get; } = [];
    public ObservableCollection<FortniteProfileStepResult> Steps { get; } = [];
    public ObservableCollection<AppliedOptimization> Changes { get; } = [];
    public bool HasPreview => preview is not null;
    public bool IsInstalled => preview?.Installation.Installed == true;
    public string InstallPath => preview?.Installation.InstallDirectory ?? "No se encontró una instalación de Fortnite.";
    public string SettingsPath => preview?.Installation.SettingsPath ?? "La configuración del juego aún no está disponible.";
    public string InstallationMessage => preview?.Installation.Message ?? "Detección automática desde Epic Games; también puedes elegir la carpeta.";
    public bool IsGameRunning => preview?.Installation.IsRunning == true;
    public bool HasChanges => Changes.Count > 0;
    public bool HasResults => Steps.Count > 0;
    public IReadOnlyList<FortniteProfileStepResult> FailedSteps => Steps.Where(step => !step.Success).ToList();
    public bool HasFailedSteps => Steps.Any(step => !step.Success);
    public bool HasActionError => actionError;
    public bool IsApplying => applying;
    public bool IsOptimizing => optimizing || applying;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public string Status { get => status; private set { if (Set(ref status, value)) Changed(nameof(HasStatus)); } }
    public string ProgressTitle { get => progressTitle; private set => Set(ref progressTitle, value); }
    public string ProgressLabel => $"{completedSteps}/{totalSteps} pasos terminados";
    public double ProgressPercent => totalSteps > 0 ? 100d * completedSteps / totalSteps : 0;
    public string SelectionSummary => $"{Options.Count(o => o.Option.Available && o.Selected)} de {Options.Count(o => o.Option.Available)} ajustes disponibles seleccionados";
    public AppliedOptimization? SelectedChange { get => selectedChange; set { Set(ref selectedChange, value); CommandManager.InvalidateRequerySuggested(); } }

    internal Task<bool> DetectAsync() => PreviewAsync(null);

    private Task<bool> ChooseFolderAsync()
    {
        var directory = chooseFolder();
        return directory is null ? Task.FromResult(false) : PreviewAsync(directory);
    }

    private Task<bool> PreviewAsync(string? directory) => run("Detectando Fortnite y preparando ajustes…", async token =>
    {
        preview = null; Options.Clear(); Steps.Clear(); SetError(false); Changed(null);
        manualDirectory = directory;
        try
        {
            await RefreshPreviewAsync(token, false);
            Status = "";
            Changed(nameof(Status));
        }
        catch (OperationCanceledException) { Status = "Detección cancelada. Vuelve a detectar Fortnite antes de optimizar."; SetError(true); throw; }
        catch (Exception ex) { Status = $"No se pudo detectar Fortnite: {ex.Message}"; SetError(true); throw; }
    });

    internal async Task<bool> OptimizeAsync()
    {
        if (!canRun() || !confirmOptimization()) return false;
        return await run("Aplicando ajustes…", async token =>
        {
            optimizing = true; Changed(nameof(IsOptimizing)); Status = "Aplicando ajustes…"; SetError(false);
            try
            {
                await RefreshPreviewAsync(token, false);
                if (IsGameRunning)
                { Status = "Cierra Fortnite y vuelve a pulsar Aplicar ajustes."; SetError(true); return; }
                if (!Options.Any(option => option.Option.Available && option.Selected))
                { Status = "No hay ajustes disponibles para aplicar."; SetError(true); return; }
                await ApplySelectedAsync(token, true);
                if (!HasActionError) Status = "Ajustes aplicados.";
            }
            catch (OperationCanceledException) { Status = "Optimización cancelada."; SetError(true); throw; }
            catch (Exception) { Status = "No se pudo completar la optimización. Vuelve a intentarlo."; SetError(true); }
            finally { optimizing = false; Changed(nameof(IsOptimizing)); }
        });
    }

    internal Task<bool> ApplyAsync() => run("Aplicando ajustes de Fortnite…", token => ApplySelectedAsync(token, false));

    private async Task ApplySelectedAsync(CancellationToken token, bool simple, bool gameOnly = false)
    {
        var selectedIds = Options.Where(o => o.Option.Available && o.Selected && (!gameOnly || o.Option.Id is "fortnite-settings" or "gpu-preference")).Select(o => o.Option.Id).ToHashSet();
        var optionIds = preview?.Options.Where(option => selectedIds.Contains(option.Id)).Select(option => option.Id).ToArray() ?? [];
        if (!HasPreview || optionIds.Length == 0) return;
        Steps.Clear(); applying = true; completedSteps = 0; totalSteps = optionIds.Length;
        ProgressTitle = "Preparando los ajustes seleccionados"; SetError(false); Changed(null);
        var generation = ++progressGeneration;
        var progress = new Progress<FortniteProfileProgress>(value =>
        {
            if (!applying || generation != progressGeneration) return;
            completedSteps = value.Completed; totalSteps = value.Total; ProgressTitle = value.Title;
            Changed(nameof(ProgressPercent)); Changed(nameof(ProgressLabel));
        });
        try
        {
            var result = await service.ApplyAsync(optionIds, progress, token);
            foreach (var step in result.Steps) Steps.Add(step);
            var incomplete = result.Cancelled || result.Steps.Any(s => !s.Success) ||
                simple && (result.Steps.Count != optionIds.Length || !result.Steps.Select(step => step.Id).ToHashSet().SetEquals(optionIds));
            SetError(incomplete);
            Status = simple ? result.Cancelled ? "Optimización cancelada." : incomplete ? "No se pudo completar la optimización. Vuelve a intentarlo." : "Finalizando…" : result.Message;
        }
        catch (OperationCanceledException) { Status = "Perfil cancelado. Los cambios que se alcanzaron a guardar siguen disponibles para restaurarlos."; SetError(true); throw; }
        catch (Exception ex) { Status = $"No se completó el perfil: {ex.Message}"; SetError(true); throw; }
        finally
        {
            applying = false; progressGeneration++; Changed(null);
            await RefreshAfterChangeAsync();
        }
    }

    internal Task<bool> UndoAsync() => run("Restaurando el ajuste seleccionado de Fortnite…", async token =>
    {
        if (SelectedChange is not { CanUndo: true } change) return;
        try { var result = await service.UndoAsync(change.Id, token); Status = result.Message; SetError(!result.Success); }
        catch (OperationCanceledException) { Status = "Restauración cancelada. Revisa el estado y el respaldo conservado."; SetError(true); throw; }
        catch (Exception ex) { Status = $"No se pudo restaurar: {ex.Message}"; SetError(true); throw; }
        finally { await RefreshAfterChangeAsync(); }
    });

    private async Task RefreshAfterChangeAsync()
    {
        try { await RefreshPreviewAsync(CancellationToken.None, true); }
        catch (Exception ex) { preview = null; Options.Clear(); Changed(null); Status += $" No se pudo actualizar el perfil: {ex.Message}. Pulsa Detectar Fortnite."; SetError(true); }
        try { await refreshTools(CancellationToken.None); }
        catch (Exception ex) { Status += $" No se pudieron actualizar los ajustes: {ex.Message}. Pulsa Actualizar ajustes."; SetError(true); }
    }

    private async Task RefreshPreviewAsync(CancellationToken token, bool keepSelection)
    {
        var selected = keepSelection ? Options.ToDictionary(o => o.Option.Id, o => o.Selected) : null;
        var result = await service.PreviewAsync(manualDirectory, token);
        token.ThrowIfCancellationRequested(); preview = result;
        Options.Clear();
        foreach (var option in result.Options.OrderBy(option => option.Id switch
        { "fortnite-settings" => 0, "gpu-preference" => 1, "power-mode" => 2, "power" => 3, "background-apps" => 4, "browser-background" => 5, "visual-effects" => 6, _ => 7 }))
        {
            var row = new FortniteOptionChoice(option) { Selected = option.Available && (selected?.GetValueOrDefault(option.Id, option.SelectedByDefault) ?? option.SelectedByDefault) };
            row.PropertyChanged += (_, _) => { Changed(nameof(SelectionSummary)); CommandManager.InvalidateRequerySuggested(); };
            Options.Add(row);
        }
        var previousId = SelectedChange?.Id;
        Changes.Clear(); foreach (var change in result.Changes.OrderByDescending(c => c.AppliedAt)) Changes.Add(change with { Title = change.Title.Replace("WinUtil · ", "", StringComparison.Ordinal) });
        SelectedChange = Changes.FirstOrDefault(c => c.Id == previousId);
        Changed(null);
    }

    private void SetError(bool value) { actionError = value; Changed(nameof(HasActionError)); }
}

public sealed class FortniteOptionChoice(FortniteProfileOption option) : ObservableObject
{
    private bool selected;
    public FortniteProfileOption Option { get; } = option;
    public bool Selected { get => selected; set { if (Set(ref selected, value && Option.Available)) CommandManager.InvalidateRequerySuggested(); } }
}
