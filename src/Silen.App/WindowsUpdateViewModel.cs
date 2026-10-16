using System.Windows.Input;
using Silen.Core;
using Silen.Windows;

namespace Silen.App;

public sealed class WindowsUpdateViewModel : ObservableObject
{
    private readonly INativeOptimizationService service;
    private readonly Func<string, Func<CancellationToken, Task>, Task<bool>> run;
    private readonly Func<bool> elevated;
    private readonly Action requestElevation;
    private readonly Func<CancellationToken, Task> refreshBackups;
    private readonly Func<string, string, bool> confirm;
    private WindowsUpdateHealth health = WindowsUpdateHealth.Unknown("Comprueba la pausa y el servicio antes de buscar drivers.");
    private string status = "";
    internal WindowsUpdateViewModel(INativeOptimizationService service, Func<string, Func<CancellationToken, Task>, Task<bool>> run,
        Func<bool> canRun, Func<bool> elevated, Action requestElevation, Func<CancellationToken, Task> refreshBackups, Func<string, string, bool>? confirm = null)
    {
        this.service = service; this.run = run; this.elevated = elevated; this.requestElevation = requestElevation; this.refreshBackups = refreshBackups;
        this.confirm = confirm ?? ((_, _) => false);
        RefreshCommand = new AsyncCommand(_ => RefreshAsync(), _ => canRun(), ex => SetStatus(ex.Message));
        ResumeCommand = new AsyncCommand(_ => ResumeAsync(), _ => canRun() && NeedsRecovery, ex => SetStatus(ex.Message));
    }
    public ICommand RefreshCommand { get; }
    public ICommand ResumeCommand { get; }
    public WindowsUpdateHealth Health => health;
    public string HealthMessage => health.Message;
    public string Status => status;
    public bool HasStatus => status.Length > 0;
    public bool NeedsRecovery => health.NeedsRecovery;
    public string ResumeLabel => health.Paused ? "Reanudar actualizaciones" : health.ServiceEnabled == false ? "Activar Windows Update" : "Iniciar servicio de actualización";
    internal void SetHealth(WindowsUpdateHealth value) { health = value; Changed(null); CommandManager.InvalidateRequerySuggested(); }
    internal Task<bool> RefreshAsync() => run("Comprobando Windows Update…", async token => { SetHealth(await service.InspectWindowsUpdateAsync(token)); SetStatus(""); });
    internal Task<bool> ResumeAsync() => run("Revisando la reanudación de Windows Update…", async token =>
    {
        if (!health.NeedsRecovery) return;
        if (!confirm("Reanudar Windows Update", "Se intentará retirar la pausa local e iniciar el servicio de Windows Update. Se guardará el estado anterior; no se eliminarán políticas de la organización. Windows puede pedir permiso para esta operación. ¿Continuar?")) return;
        if (!elevated() && service is not SystemActionOptimizationService)
        {
            try { requestElevation(); SetStatus("Silen se abrió en Drivers con permisos. Revisa el estado y pulsa reanudar en esa ventana. No se aplicaron cambios aquí."); }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { SetStatus("Solicitud de permiso cancelada. No se modificó Windows Update."); }
            return;
        }
        try { var result = await service.ResumeWindowsUpdateAsync(token); SetStatus(result.Message); }
        finally
        {
            try { SetHealth(await service.InspectWindowsUpdateAsync(CancellationToken.None)); await refreshBackups(CancellationToken.None); }
            catch (Exception ex) { SetStatus(Status + $" No se pudo actualizar el estado o respaldo: {ex.Message}"); }
        }
    });
    private void SetStatus(string value) { status = value; Changed(null); }
}
