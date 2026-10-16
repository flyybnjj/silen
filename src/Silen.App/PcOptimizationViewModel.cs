using System.Collections.ObjectModel;
using System.Windows.Input;
using Silen.Windows;
using Silen.Core;

namespace Silen.App;

public sealed partial class PcOptimizationViewModel : ObservableObject
{
    private readonly IPcOptimizationService service;
    private readonly Func<string, Func<CancellationToken, Task>, Task<bool>> run;
    private readonly Func<bool> canRun;
    private readonly Func<IReadOnlyList<PcOptimizationOption>, bool> confirm;
    private readonly Func<CancellationToken, Task> refreshTools;
    private readonly IApplicationTimerService applicationTimer;
    private readonly SummaryRecoveryViewModel recovery;
    private bool hasPreview, preparing, applying, error;
    private string status = "", progressTitle = "";
    private int completed, total, generation;

    internal PcOptimizationViewModel(IPcOptimizationService service, Func<string, Func<CancellationToken, Task>, Task<bool>> run,
        Func<bool> canRun, Func<IReadOnlyList<PcOptimizationOption>, bool> confirm, Func<CancellationToken, Task> refreshTools, SummaryRecoveryViewModel recovery,
        IInstalledProductCatalog? products = null, Func<string?>? chooseExecutable = null, IApplicationTimerService? applicationTimer = null)
    {
        this.service = service; this.run = run; this.canRun = canRun; this.confirm = confirm; this.refreshTools = refreshTools;
        this.recovery = recovery;
        recovery.Restored += token => RefreshAsync(token, true);
        this.applicationTimer = applicationTimer ?? new InactiveApplicationTimerService(); service.AttachTimerService(this.applicationTimer);
        DisableTimerCommand = new AsyncCommand(_ => DisableTimerAsync(), _ => canRun() && HasTimerProfile, ex => SetStatus(ex.Message, true));
        InitializeProducts(products ?? new InstalledProductCatalog(), chooseExecutable ?? (() => null));
        PrepareCommand = new AsyncCommand(_ => PrepareAsync(), _ => canRun(), ex => SetStatus(ex.Message, true));
        ApplyCommand = new AsyncCommand(_ => ApplyAsync(), _ => canRun() && HasPreview && Options.Any(row => row.Selected), ex => SetStatus(ex.Message, true));
        RecommendedCommand = new RelayCommand(_ => Select(true), _ => canRun() && HasPreview);
        ClearSelectionCommand = new RelayCommand(_ => Select(false), _ => canRun() && HasPreview);
    }
    public ICommand PrepareCommand { get; }
    public ICommand DisableTimerCommand { get; }
    public string ApplicationTimerStatus => applicationTimer.Status;
    public bool HasTimerProfile => SelectedExecutable is { } path && applicationTimer.IsEnabled(path);
    internal void RefreshTimerStatus() { Changed(nameof(ApplicationTimerStatus)); Changed(nameof(HasTimerProfile)); }
    private Task<bool> DisableTimerAsync() => run("Desactivando temporizador del producto…", async token =>
    {
        if (SelectedExecutable is not { } path) return;
        var result = await applicationTimer.SetEnabledAsync(path, false, token); SetStatus(result.Message, !result.Success);
        RefreshTimerStatus(); if (HasPreview) await RefreshAsync(token, true);
    });
    public ICommand ApplyCommand { get; }
    public ICommand RecommendedCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ObservableCollection<PcOptimizationChoice> Options { get; } = [];
    public ObservableCollection<PcOptimizationStep> Steps { get; } = [];
    internal event Action<IReadOnlyList<string>>? ChangesApplied;
    internal event Action<IReadOnlyList<string>>? PartialChangesApplied;
    public bool HasPreview => hasPreview;
    public bool HasResults => Steps.Count > 0;
    public bool NeedsAdministrator => Options.Any(row => !row.Option.Available && row.Option.Status.Contains("administrador", StringComparison.OrdinalIgnoreCase));
    public bool HasActionError => error;
    public bool HasStatus => !string.IsNullOrWhiteSpace(status);
    public bool IsPreparing => preparing;
    public bool IsOptimizing => preparing || applying;
    public string Status => status;
    public string ProgressTitle => progressTitle;
    public string ProgressLabel => preparing ? "Consultando disponibilidad…" : $"{completed}/{total} {SpanishText.Form(total, "paso terminado", "pasos terminados")}";
    public double ProgressPercent => total == 0 ? 0 : 100d * completed / total;
    public string SelectionSummary => $"{SpanishText.Count(Options.Count(row => row.Selected), "seleccionado", "seleccionados")} · {SpanishText.Count(Options.Count(row => row.Option.Available), "disponible", "disponibles")} · {SpanishText.Count(Options.Count(row => row.Option.AlreadyApplied), "ya aplicado", "ya aplicados")}";

    internal Task<bool> PrepareAsync() => run("Preparando ajustes del PC…", async token =>
    {
        preparing = true; Steps.Clear(); completed = 0; total = 0; progressTitle = "Consultando ajustes y permisos";
        SetStatus("Preparando opciones del PC…", false); Changed(null);
        try
        {
            await RefreshAsync(token, HasPreview);
            await recovery.RefreshAsync(token);
            SetStatus(Options.Any(row => row.Option.SelectedByDefault)
                ? "Revisa la selección. Prepararla no cambia Windows."
                : "No hay ajustes recomendados pendientes disponibles. Puedes revisar las opciones manuales.", false);
        }
        catch (OperationCanceledException) { SetStatus("Preparación cancelada. No se cambió Windows.", false); throw; }
        catch (Exception ex) { SetStatus($"No se pudo preparar la selección: {ex.Message}", true); throw; }
        finally { preparing = false; Changed(null); }
    });

    internal async Task<bool> ApplyAsync()
    {
        var selected = Options.Where(row => row.Selected && row.Option.Available).Select(row => row.Option).ToArray();
        if (!canRun() || !HasPreview || selected.Length == 0 || !confirm(selected)) return false;
        return await run("Optimizando el PC…", async token =>
        {
            applying = true; completed = 0; total = selected.Length; Steps.Clear(); progressTitle = "Comprobando disponibilidad y permisos";
            SetStatus("Aplicando la selección…", false); Changed(null);
            var currentGeneration = ++generation;
            var progress = new Progress<PcOptimizationProgress>(value =>
            {
                if (!applying || currentGeneration != generation) return;
                completed = value.Completed; total = value.Total; progressTitle = value.Title; Changed(null);
            });
            try
            {
                if (!await recovery.PrepareBatchAsync(token))
                {
                    SetStatus("No se inició el lote. Revisa el estado de Recuperación antes de reintentar.", false);
                    return;
                }
                token.ThrowIfCancellationRequested();
                var result = await service.ApplyAsync(selected.Select(option => option.Id).ToArray(), progress, token);
                foreach (var step in result.Steps) Steps.Add(step);
                var finished = result.Steps.Where(step => step.Success && !step.Skipped).Select(step => step.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var row in Options.Where(row => finished.Contains(row.Option.Id))) row.Selected = false;
                var changed = result.Steps.Where(step => step.Success && step.Changed && !step.Skipped).Select(step => step.Id).ToArray();
                if (changed.Length > 0) ChangesApplied?.Invoke(changed);
                var partial = result.Steps.Where(step => !step.Success && step.Changed && !step.Skipped).Select(step => step.Id).ToArray();
                if (partial.Length > 0) PartialChangesApplied?.Invoke(partial);
                completed = Steps.Count; SetStatus(result.Message + (partial.Length > 0 ? $" {SpanishText.Count(partial.Length, "cambio parcial", "cambios parciales")}: revisa los respaldos; no se confirmó su aplicación completa ni su beneficio." : ""), result.Cancelled || result.Failed > 0 || result.Skipped > 0);
            }
            catch (OperationCanceledException) { SetStatus("Proceso cancelado. Revisa los cambios guardados antes de volver a aplicar.", true); throw; }
            catch (Exception ex) { SetStatus($"No se completó el proceso: {ex.Message}. Revisa los respaldos en Ajustes.", true); throw; }
            finally
            {
                applying = false; generation++;
                try { await refreshTools(CancellationToken.None); }
                catch (Exception ex) { SetStatus(Status + $" No se pudieron actualizar los respaldos: {ex.Message}", true); }
                try { await RefreshAsync(CancellationToken.None, true); }
                catch (Exception ex) { SetStatus(Status + $" Vuelve a preparar la selección: {ex.Message}", true); }
                await recovery.RefreshAsync(CancellationToken.None);
                Changed(null);
            }
        });
    }

    private async Task RefreshAsync(CancellationToken token, bool keepSelection)
    {
        var selected = keepSelection ? Options.ToDictionary(row => row.Option.Id, row => row.Selected) : null;
        hasPreview = false; Options.Clear(); Changed(null);
        var preview = await service.PreviewAsync(token);
        token.ThrowIfCancellationRequested();
        foreach (var option in preview.Options)
        {
            var row = new PcOptimizationChoice(option) { Selected = selected?.GetValueOrDefault(option.Id, option.SelectedByDefault) ?? option.SelectedByDefault };
            row.PropertyChanged += (_, _) => { Changed(nameof(SelectionSummary)); CommandManager.InvalidateRequerySuggested(); };
            Options.Add(row);
        }
        hasPreview = true; Changed(null); CommandManager.InvalidateRequerySuggested();
    }
    private void Select(bool recommended)
    {
        foreach (var row in Options) row.Selected = recommended && row.Option.SelectedByDefault;
    }
    private void SetStatus(string message, bool failed) { status = message; error = failed; Changed(null); Changed(nameof(Status)); }
}

public sealed class PcOptimizationChoice(PcOptimizationOption option) : ObservableObject
{
    private bool selected;
    public PcOptimizationOption Option { get; } = option;
    public bool Selected { get => selected; set => Set(ref selected, value && Option.Available); }
}
