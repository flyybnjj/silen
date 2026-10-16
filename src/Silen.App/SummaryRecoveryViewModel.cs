using System.Windows.Input;
using Silen.Windows;

namespace Silen.App;

public sealed class SummaryRecoveryViewModel : ObservableObject
{
    private readonly IRestorePointService points;
    private readonly INativeOptimizationService native;
    private readonly Func<RestorePointResult, bool> confirmFallback;
    private readonly Func<string, string, bool> confirm;
    private readonly Func<string, Func<CancellationToken, Task>, Task<bool>> run;
    private readonly Func<CancellationToken, Task> refreshTools;
    private AppliedOptimization? latest;
    private bool hasBatch, pointError, undoError, needsPointDecision;
    private string pointStatus = "", undoStatus = "No hay un respaldo verificado para deshacer.";

    internal SummaryRecoveryViewModel(IRestorePointService points, INativeOptimizationService native,
        Func<RestorePointResult, bool> confirmFallback, Func<string, string, bool> confirm,
        Func<string, Func<CancellationToken, Task>, Task<bool>> run, Func<bool> canRun,
        Func<CancellationToken, Task> refreshTools, Action openProtection)
    {
        this.points = points; this.native = native; this.confirmFallback = confirmFallback; this.confirm = confirm;
        this.run = run; this.refreshTools = refreshTools;
        UndoCommand = new AsyncCommand(_ => UndoAsync(), _ => canRun() && latest is { CanUndo: true }, ex => SetUndoStatus(ex.Message, true));
        OpenProtectionCommand = new RelayCommand(_ => { try { openProtection(); } catch (Exception ex) { SetUndoStatus("No se pudo abrir Protección del sistema: " + ex.Message, true); } }, _ => canRun());
    }
    public ICommand UndoCommand { get; }
    public ICommand OpenProtectionCommand { get; }
    public bool HasBatch => hasBatch;
    public bool HasPointError => pointError;
    public bool CanOpenProtection => hasBatch && needsPointDecision;
    public bool HasUndoError => undoError;
    public string PointStatus => pointStatus;
    public string UndoStatus => undoStatus;
    public string LastChangeLabel => latest is { } change ? $"Último cambio respaldado: {change.Title.Replace("WinUtil · ", "", StringComparison.Ordinal)}." : "Sin cambio respaldado disponible.";
    internal event Func<CancellationToken, Task>? Restored;

    internal async Task<bool> PrepareBatchAsync(CancellationToken token)
    {
        hasBatch = true; pointError = false; needsPointDecision = false; pointStatus = "Solicitando y verificando el punto de restauración con Windows…"; Changed(null);
        RestorePointResult result;
        try { result = await points.PrepareAsync(token); }
        catch (OperationCanceledException) { result = new(RestorePointState.Cancelled); }
        catch (Exception) { result = new(RestorePointState.Failed); }
        pointStatus = result.Message;
        pointError = result.State is RestorePointState.Disabled or RestorePointState.Unavailable or RestorePointState.Failed;
        needsPointDecision = !result.CanContinueAutomatically && result.State != RestorePointState.Cancelled;
        Changed(null);
        if (token.IsCancellationRequested || result.State == RestorePointState.Cancelled) return false;
        if (result.CanContinueAutomatically) return true;
        if (!confirmFallback(result)) { pointStatus += " No se inició el lote."; Changed(null); return false; }
        token.ThrowIfCancellationRequested();
        pointStatus += " Elegiste continuar sin un punto nuevo verificado, con el respaldo previo de cada ajuste en el journal de Silen.";
        Changed(null);
        return true;
    }

    internal async Task RefreshAsync(CancellationToken token)
    {
        try
        {
            var overview = await native.InspectAsync(token);
            undoError = false;
            latest = overview.Changes.Where(change => !change.Restored).OrderByDescending(change => change.AppliedAt).FirstOrDefault();
            undoStatus = latest is null ? "No hay un respaldo verificado para deshacer." : latest.CanUndo
                ? "Deshace un solo cambio. Se comprobará el respaldo y se conservarán los cambios externos."
                : "El último respaldo no permite restauración con los permisos o el estado actuales. No se elegirá otro cambio a escondidas.";
        }
        catch (Exception) { latest = null; undoError = true; undoStatus = "No se pudieron consultar los respaldos. Deshacer queda deshabilitado; vuelve a preparar la selección para reintentar."; }
        Changed(null); CommandManager.InvalidateRequerySuggested();
    }

    internal Task<bool> UndoAsync()
    {
        var chosen = latest;
        if (chosen is not { CanUndo: true } || !confirm("Deshacer último cambio", $"¿Restaurar «{chosen.Title.Replace("WinUtil · ", "", StringComparison.Ordinal)}» a su estado anterior? Se restaurará solo este ajuste, no todo el lote ni Windows. Si otra aplicación lo cambió, se conservará ese cambio."))
            return Task.FromResult(false);
        return run("Deshaciendo el último cambio respaldado…", async token =>
        {
            var message = "No se completó la restauración. El respaldo se volverá a consultar.";
            var failed = true;
            try
            {
                var overview = await native.InspectAsync(token);
                var current = overview.Changes.Where(change => !change.Restored).OrderByDescending(change => change.AppliedAt).FirstOrDefault();
                if (current?.Id != chosen.Id || !current.CanUndo) { message = "El último respaldo cambió o dejó de estar disponible. Revisa el cambio mostrado antes de confirmar de nuevo."; return; }
                var outcome = await native.UndoAsync(chosen.Id, token);
                failed = !outcome.Success;
                message = (!outcome.Success && outcome.Changed ? "Restauración parcial. " : "") + outcome.Message;
                if (Restored is { } restored) await restored(CancellationToken.None);
            }
            finally
            {
                try { await refreshTools(CancellationToken.None); }
                catch (Exception) { message += " No se pudo actualizar la lista de ajustes."; failed = true; }
                await RefreshAsync(CancellationToken.None);
                if (undoError) { message += " " + undoStatus; failed = true; }
                SetUndoStatus(message, failed);
            }
        });
    }
    private void SetUndoStatus(string message, bool failed = false) { undoStatus = message; undoError = failed; Changed(nameof(UndoStatus)); Changed(nameof(HasUndoError)); }
}
