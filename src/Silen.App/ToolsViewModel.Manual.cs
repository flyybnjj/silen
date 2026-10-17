using System.Collections.ObjectModel;
using System.Windows.Input;
using Silen.Core;
using Silen.Windows;

namespace Silen.App;

public enum ManualChangeKind { Toggle, Power, Startup }
public sealed record ManualChange(string Id, string Title, string Description, ManualChangeKind Kind, bool Enabled = true, string? Target = null, string? ExpectedCommand = null)
{
    public string Detail => (Kind == ManualChangeKind.Toggle && !Enabled ? "Restaurar o desactivar. " : "") + Description + " " + RestartPlan.ActivationText(Id);
}
public sealed partial class ToolsViewModel
{
    private Func<string, string, bool> confirmManual = (_, _) => false;
    public ObservableCollection<ManualChange> PendingManual { get; } = [];
    public bool HasPendingManual => PendingManual.Count > 0;
    public string ManualSummary => $"{SpanishText.Count(PendingManual.Count, "cambio pendiente", "cambios pendientes")}. Añadir opciones no modifica Windows.";
    public ICommand ApplyManualCommand { get; private set; } = null!;
    public ICommand RemoveManualCommand { get; private set; } = null!;
    public ICommand LoadSystemBackupsCommand { get; private set; } = null!;
    internal event Action<IReadOnlyList<string>>? ChangesApplied;
    internal event Action<IReadOnlyList<string>>? PartialChangesApplied;
    private void InitializeManual(Func<string, string, bool> confirm)
    {
        confirmManual = confirm;
        ApplyManualCommand = Command(_ => ApplyManualAsync(), _ => CanApplySelection() && HasPendingManual);
        RemoveManualCommand = new RelayCommand(parameter => { if (parameter is ManualChange item) PendingManual.Remove(item); Changed(null); }, _ => canRun());
        LoadSystemBackupsCommand = Command(_ => LoadSystemBackupsAsync(), _ => service is SystemActionOptimizationService);
    }
    private Task QueueManual(ManualChange change)
    {
        var previous = PendingManual.FirstOrDefault(item => item.Id == change.Id);
        if (previous is not null) PendingManual.Remove(previous);
        PendingManual.Add(change); Status = "Cambio añadido. Revisa la selección y pulsa Aplicar selección manual.";
        Changed(null); CommandManager.InvalidateRequerySuggested(); return Task.CompletedTask;
    }
    internal async Task<bool> ApplyManualAsync()
    {
        var selection = PendingManual.ToArray();
        if (!CanApplySelection() || selection.Length == 0 || !confirmManual("Aplicar selección manual", string.Join("\n\n", selection.Select(item => item.Title + ": " + item.Detail)))) return false;
        return await run("Aplicando selección manual…", async token =>
        {
            var applied = new List<string>(); var partial = new List<string>(); var messages = new List<string>(); var failed = 0; var cancelled = false;
            try
            {
                var finished = 0;
                foreach (var item in selection)
                {
                    ActivityProgress?.Invoke("Estoy aplicando tu selección.", $"{finished} de {selection.Length} pasos terminados · {item.Title}");
                    await Task.Yield();
                    token.ThrowIfCancellationRequested();
                    OptimizationOutcome result;
                    try
                    {
                        result = item.Kind switch
                        {
                            ManualChangeKind.Toggle => await service.SetToggleAsync(item.Id, item.Enabled, token),
                            ManualChangeKind.Power => await service.SetPowerSchemeAsync(item.Target!, token),
                            ManualChangeKind.Startup => await ApplyManualStartupAsync(item, token),
                            _ => new(false, "Operación no permitida.")
                        };
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { result = new(false, "No se completó. Actualiza estado y revisa los respaldos antes de reintentar."); }
                    finished++;
                    messages.Add(item.Title + ": " + result.Message);
                    if (!result.Success) failed++;
                    if (result.Success) PendingManual.Remove(item);
                    if (result.Success && result.Changed) applied.Add(item.Id);
                    else if (!result.Success && result.Changed) partial.Add(item.Id);
                }
            }
            catch (OperationCanceledException) { cancelled = true; throw; }
            finally
            {
                if (applied.Count > 0) ChangesApplied?.Invoke(applied);
                if (partial.Count > 0) PartialChangesApplied?.Invoke(partial);
                actionError = failed > 0;
                ActivityResult?.Invoke(cancelled ? "He detenido la selección." : failed > 0 ? "Hay pasos que requieren revisión." : "He terminado tu selección.", $"{SpanishText.Count(applied.Count, "cambio completado", "cambios completados")} · {SpanishText.Count(partial.Count, "cambio parcial", "cambios parciales")} · {SpanishText.Count(failed, "paso con error", "pasos con error")}. Consulta los respaldos antes de reintentar.");
                Status = $"{SpanishText.Count(applied.Count, "cambio completado", "cambios completados")}; {SpanishText.Count(partial.Count, "cambio parcial", "cambios parciales")}; {SpanishText.Count(failed, "paso con error", "pasos con error")}. " + (partial.Count > 0 ? "Revisa los respaldos de los cambios parciales antes de reintentar. " : "") + (cancelled || token.IsCancellationRequested ? "Operación cancelada. " : "") + string.Join("\n", messages);
                try { await RefreshStateAsync(CancellationToken.None); } catch { Status += " Revisa los respaldos al actualizar el estado."; }
                Changed(null);
            }
        });
    }
    private async Task<OptimizationOutcome> ApplyManualStartupAsync(ManualChange item, CancellationToken token)
    {
        var current = (await service.InspectAsync(token)).StartupEntries.FirstOrDefault(entry => entry.Id == item.Target);
        if (current is null || current.Command != item.ExpectedCommand) return new(false, "La entrada cambió desde la selección. Se conservó; vuelve a elegirla.");
        return !item.Enabled ? await service.DisableStartupCheckedAsync(item.Target!, item.ExpectedCommand!, token) : await service.SetStartupEnabledAsync(item.Target!, true, token);
    }
    private Task<bool> LoadSystemBackupsAsync() => run("Leyendo respaldos protegidos del sistema…", async token =>
    {
        if (service is not SystemActionOptimizationService proxy || !confirmManual("Respaldos del sistema", "Windows pedirá permiso para leer los respaldos protegidos. Esta operación no aplica ni restaura ajustes.")) return;
        var result = await proxy.RefreshSystemHistoryAsync(token); Status = result.Message; await RefreshStateAsync(token);
    });
}
