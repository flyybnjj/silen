using Silen.Core;

namespace Silen.Windows;

public sealed partial class NativeOptimizationService
{
    internal WindowsUpdateHealth ReadWindowsUpdateStatus() => WindowsUpdateDiagnostics.Read(platform, clock());
    public Task<WindowsUpdateHealth> InspectWindowsUpdateAsync(CancellationToken cancellationToken = default) => Task.Run(ReadWindowsUpdateStatus, cancellationToken);
    private ServiceSnapshot ReadJournalService(string name) => (name == "wuauserv" ? platform.ReadUpdateService() : platform.ReadService(name))
        ?? throw new InvalidOperationException("El servicio ya no está instalado.");

    public async Task<OptimizationOutcome> ResumeWindowsUpdateAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(_ =>
        {
            var health = ReadWindowsUpdateStatus();
            if (!health.Known || health.PauseManaged) throw new InvalidOperationException(health.Message);
            var state = platform.ReadUpdateService() ?? throw new InvalidOperationException("Windows Update no está instalado.");
            var values = new List<OptimizationValueChange>();
            if (health.Paused)
                foreach (var name in WindowsUpdateDiagnostics.PauseValues)
                {
                    var before = platform.ReadValue(WindowsUpdateDiagnostics.UxKey, name);
                    if (before.Exists)
                    {
                        if (!platform.CanWriteKey(WindowsUpdateDiagnostics.UxKey)) throw new UnauthorizedAccessException("Se necesitan permisos para retirar la pausa local.");
                        values.Add(new("Registry", WindowsUpdateDiagnostics.UxKey, name, before, RegistryValueState.Absent));
                    }
                }
            if ((state.StartType == 4 || !state.Running) && !platform.CanManageUpdateService()) throw new UnauthorizedAccessException("Se necesitan permisos para activar el servicio de Windows Update.");
            if (state.StartType == 4) values.Add(new("ServiceStart", "wuauserv", "", RegistryValueState.Dword(4), RegistryValueState.Dword(3)));
            if (!state.Running) values.Add(new("ServiceRunning", "wuauserv", "", RegistryValueState.Dword(0), RegistryValueState.Dword(1)));
            return NewEntry("windows-update-resume", "Windows Update · Reanudar pausa local y servicio", values);
        }, cancellationToken);
        if (!result.Success) return result;
        var after = ReadWindowsUpdateStatus();
        for (var attempt = 0; result.Changed && after.Paused && attempt < 5 && !cancellationToken.IsCancellationRequested; attempt++)
        { await Task.Delay(150, CancellationToken.None); after = ReadWindowsUpdateStatus(); }
        return new(after.CanSearch, after.CanSearch ? "Pausa local retirada y servicio en ejecución, comprobados. Puedes volver a buscar drivers. Los cambios se pueden restaurar en Ajustes."
            : "No se confirmó la reanudación de Windows Update. " + after.Message + " Revisa los respaldos disponibles.", result.Changed);
    }
}
