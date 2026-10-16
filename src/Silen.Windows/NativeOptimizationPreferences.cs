namespace Silen.Windows;

public sealed partial class NativeOptimizationService
{
    private IEnumerable<ToggleSetting> InspectPreferences(List<OptimizationJournalEntry> entries)
    {
        foreach (var group in PerformanceTweaks.Groups)
        {
            ToggleSetting toggle;
            try
            {
                var applied = group.Values.All(value => platform.ReadValue(value.Key, value.Name).Same(RegistryValueState.Dword(value.Target)));
                var chromeMachinePolicy = group.Id == "chrome-background" ? ReadMachineChromePolicy() : RegistryValueState.Absent;
                if (chromeMachinePolicy.Exists) applied = chromeMachinePolicy.Same(RegistryValueState.Dword(0));
                var allowed = applied || CanApplyPerformanceTweak(group.Id) && (!group.RequiresAc || onAcPower() == true);
                var restore = entries.Any(entry => entry.ToolId == group.Id && CanUndo(entry));
                var status = applied ? restore ? "Aplicado · respaldo disponible" : "Estado detectado · sin respaldo de Silen" :
                    !allowed ? group.RequiresAc && onAcPower() != true ? "Conecta el equipo a corriente" : "Sin permiso de escritura; puede requerir administrador o una política lo impide" : "Disponible · se guardará el estado original";
                if (chromeMachinePolicy.Exists) status = applied ? "Segundo plano desactivado por política del equipo; se conserva" : "Una política del equipo controla Chrome; se conserva";
                else if (group.Id == "chrome-background" && !IsChromeRegistered()) status = "Chrome no está registrado o su ejecutable ya no existe";
                toggle = new(group.Id, group.Title, group.Description, applied, status, allowed, restore, group.Category, group.Recommended, true);
            }
            catch (Exception ex) when (IsExpectedFailure(ex)) { toggle = new(group.Id, group.Title, group.Description, null, FriendlyError(ex), false, false, group.Category, UsesBackupRestore: true); }
            yield return toggle;
        }
        foreach (var group in OptionalServices.Groups)
        {
            ToggleSetting toggle;
            try
            {
                var state = platform.ReadService(group.Name);
                var applied = state is { StartType: 4, Running: false };
                var recoverable = state is not { StartType: 4, Running: true };
                var available = state is not null && recoverable && (applied || platform.CanManageService(group.Name));
                var restore = entries.Any(entry => entry.ToolId == group.Id && CanUndo(entry));
                var status = state is null ? "No instalado en este equipo" :
                    $"{(state.Running ? "En ejecución" : "Detenido")} · inicio {state.StartType switch { 2 => "automático", 3 => "manual", _ => "deshabilitado" }}" +
                    (!recoverable ? " · ya deshabilitado; espera a que se detenga" : !available ? " · requiere permisos de administrador" : restore ? " · respaldo disponible" : "");
                toggle = new(group.Id, group.Title, group.Description, state is null ? null : applied, status, available, restore, "Servicios opcionales", UsesBackupRestore: true);
            }
            catch (Exception ex) when (IsExpectedFailure(ex)) { toggle = new(group.Id, group.Title, group.Description, null, FriendlyError(ex), false, false, "Servicios opcionales", UsesBackupRestore: true); }
            yield return toggle;
        }
    }

    private async Task<OptimizationOutcome> RestorePreferenceAsync(string id, CancellationToken token)
    {
        string? changeId;
        await using (var lease = await journal.LeaseAsync(token))
            changeId = (await journal.ReadAsync(token)).LastOrDefault(entry => entry.ToolId == id && entry.State != "Undone")?.Id;
        return changeId is null ? new(false, "No existe un respaldo de Silen para restaurar este ajuste. Se conservó el estado actual.") : await UndoAsync(changeId, token);
    }

    private Task<OptimizationOutcome> DisableOptionalServiceAsync(string id, CancellationToken token) => ExecuteAsync(_ =>
    {
        var group = OptionalServices.Find(id);
        var state = platform.ReadService(group.Name) ?? throw new InvalidOperationException("El servicio no está instalado en este equipo.");
        if (state is { StartType: 4, Running: true }) throw new InvalidOperationException("El servicio ya está deshabilitado pero continúa ejecutándose. Se conserva su estado para no crear una restauración imposible.");
        if ((state.StartType != 4 || state.Running) && !platform.CanManageService(group.Name))
            throw new UnauthorizedAccessException("No tienes permisos para modificar este servicio.");
        return NewEntry(id, group.Title,
        [
            new("ServiceStart", group.Name, "", RegistryValueState.Dword(state.StartType), RegistryValueState.Dword(4)),
            new("ServiceRunning", group.Name, "", RegistryValueState.Dword(state.Running ? 1 : 0), RegistryValueState.Dword(0))
        ]);
    }, token);
}
