using Microsoft.Win32;
using Silen.Core;
using System.ComponentModel;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace Silen.Windows;

public sealed partial class NativeOptimizationService : INativeOptimizationService
{
    private readonly IOptimizationPlatform platform;
    private readonly OptimizationJournal journal;
    private readonly Func<DateTimeOffset> clock;
    private readonly NativeTemporaryCleanup cleanup;
    private readonly Func<bool?> onAcPower;
    internal NativeTemporaryCleanup CreateIndependentCleanup() => cleanup.CreateIndependentBatch();

    public NativeOptimizationService() : this(new NativeOptimizationPlatform(), Path.Combine(AppDataPaths.Root, "optimization"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"), () => DateTimeOffset.UtcNow, authority: OptimizationAuthority.User) { }
    internal NativeOptimizationService(IOptimizationPlatform platform, string journalDirectory, string tempDirectory,
        Func<DateTimeOffset> clock, int maximumFiles = 5000, Func<bool?>? onAcPower = null, OptimizationAuthority authority = OptimizationAuthority.TestOnly)
    {
        this.platform = platform;
        journal = new(journalDirectory, authority);
        this.clock = clock;
        this.onAcPower = onAcPower ?? WindowsPowerStatus.ReadAc;
        cleanup = new(tempDirectory, clock, maximumFiles);
    }

    internal static NativeOptimizationService CreateSystemExecutor() => new(new NativeOptimizationPlatform(),
        ProtectedOptimizationDirectory.OpenForCurrentAdministrator(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
        () => DateTimeOffset.UtcNow, authority: OptimizationAuthority.Machine);

    public Task<OptimizationOverview> InspectAsync(CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        await using var lease = await journal.LeaseAsync(cancellationToken);
        var entries = await journal.ReadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var toggles = new List<ToggleSetting>
        {
            InspectToggle("game-mode", "Modo Juego", "Preferencia de Modo Juego de Windows para el usuario actual.", () =>
                ReadPairedBool(OptimizationTargets.GameBar, "AllowAutoGameMode", OptimizationTargets.GameBar, "AutoGameModeEnabled")),
            InspectToggle("xbox-captures", "Capturas Xbox", "Permite grabaciones y capturas de Xbox Game Bar. Desactivar no desinstala Xbox.", () =>
            {
                return ReadPairedBool(OptimizationTargets.GameDvr, "AppCaptureEnabled", OptimizationTargets.GameConfig, "GameDVR_Enabled");
            }),
            InspectToggle("animations", "Animaciones de ventanas", "Controla las animaciones del área cliente mediante la preferencia de Windows.", platform.GetAnimations)
        };
        for (var index = 0; index < toggles.Count; index++)
        {
            var toggle = toggles[index];
            var writable = toggle.Id switch
            {
                "game-mode" => platform.CanWriteKey(OptimizationTargets.GameBar),
                "xbox-captures" => platform.CanWriteKey(OptimizationTargets.GameDvr) && platform.CanWriteKey(OptimizationTargets.GameConfig),
                _ => toggle.Enabled is not null
            };
            toggles[index] = toggle with { CanEnable = toggle.CanEnable && writable, CanDisable = toggle.CanDisable && writable,
                Category = toggle.Id == "animations" ? "Respuesta del escritorio" : "Gaming",
                Status = writable ? toggle.Status : "Windows no permite modificar esta preferencia con los permisos actuales." };
        }
        toggles.AddRange(InspectPreferences(entries));
        toggles.AddRange(InspectInputPreferences(entries));
        var schemes = platform.ReadPowerSchemes();
        var startup = ReadStartup(entries);
        var changes = entries.OrderByDescending(x => x.AppliedAt).Select(x => new AppliedOptimization(x.Id, x.Title,
            x.AppliedAt, CanUndo(x), x.State switch
            {
                "Completed" => "Aplicado. Se guardó el estado anterior exacto.",
                "Undone" => "Se restauró el estado anterior.",
                "Pending" => "Operación interrumpida; el respaldo permite revisar y recuperar los valores aplicados.",
                "UndoPending" => "Restauración interrumpida; se puede volver a intentar.",
                _ => "Aplicación parcial; el respaldo conserva los valores originales."
            }, x.State == "Undone")).ToList();
        changes.AddRange(journal.UntrustedLegacyEntries.Select(entry => new AppliedOptimization("legacy-system:" + entry.Id,
            "Respaldo antiguo del sistema", entry.AppliedAt, false,
            "No restaurable: procede del historial editable del usuario. Se conservó una copia para revisión; no autoriza cambios del equipo.")));
        return new OptimizationOverview(toggles, schemes, startup, changes);
    }, cancellationToken);

    public Task<OptimizationOutcome> SetToggleAsync(string id, bool enabled, CancellationToken cancellationToken = default)
    {
        if (id is "input-filterkeys" or "input-mouse-linear")
            return enabled ? ApplyInputPreferenceAsync(id, cancellationToken) : RestorePreferenceAsync(id, cancellationToken);
        if (PerformanceTweaks.Groups.Any(group => group.Id == id))
            return enabled ? ApplyPerformanceTweakAsync(id, cancellationToken) : RestorePreferenceAsync(id, cancellationToken);
        if (OptionalServices.Groups.Any(group => group.Id == id))
            return enabled ? DisableOptionalServiceAsync(id, cancellationToken) : RestorePreferenceAsync(id, cancellationToken);
        return ExecuteAsync(entries =>
        {
            var desired = RegistryValueState.Dword(enabled ? 1 : 0);
            var values = id switch
            {
                "game-mode" => new List<OptimizationValueChange> { RegistryChange(OptimizationTargets.GameBar, "AutoGameModeEnabled", desired), RegistryChange(OptimizationTargets.GameBar, "AllowAutoGameMode", desired) },
                "xbox-captures" => [RegistryChange(OptimizationTargets.GameDvr, "AppCaptureEnabled", desired), RegistryChange(OptimizationTargets.GameConfig, "GameDVR_Enabled", desired)],
                "animations" => [new("Animations", "", "", AnimationState(), desired)],
                _ => throw new ArgumentException("Esta preferencia no está disponible.")
            };
            if (values.Any(value => value.Kind == "Registry" && !value.Previous.Same(value.Intended) && !platform.CanWriteKey(value.Key)))
                throw new UnauthorizedAccessException("No tienes permisos para modificar esta preferencia.");
            var title = id switch { "game-mode" => "Modo Juego", "xbox-captures" => "Capturas Xbox", _ => "Animaciones de ventanas" };
            return NewEntry(id, title + (enabled ? ": activar" : ": desactivar"), values);
        }, cancellationToken);
    }

    public Task<OptimizationOutcome> SetPowerSchemeAsync(string id, CancellationToken cancellationToken = default) =>
        SetPowerSchemeCheckedAsync(id, null, cancellationToken);

    internal Task<OptimizationOutcome> SetPowerSchemeCheckedAsync(string id, Func<bool>? canApply, CancellationToken cancellationToken) =>
        ExecuteAsync(entries =>
        {
            if (!Guid.TryParse(id, out var target)) throw new ArgumentException("Selecciona un plan de energía existente.");
            var scheme = platform.ReadPowerSchemes().FirstOrDefault(x => Guid.Parse(x.Id) == target)
                ?? throw new ArgumentException("El plan de energía ya no está disponible.");
            return NewEntry("power", "Plan de energía: " + scheme.Name,
                [new("Power", "", "", RegistryValueState.String(platform.GetActivePowerScheme().ToString()), RegistryValueState.String(target.ToString()))]);
        }, cancellationToken, canApply);

    public Task<OptimizationOutcome> ApplyWinUtilGameModeAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(_ => NewEntry("game-mode", "WinUtil · Modo Juego: activar",
            WinUtilGameMode.Read().Select(value => RegistryChange(OptimizationTargets.GameBar, value.Name,
                RegistryValueState.Dword(value.Value))).ToList()), cancellationToken);

    public bool CanApplyPerformanceTweak(string id)
    {
        try
        {
            if (id == "chrome-background" && (!IsChromeRegistered() || ReadMachineChromePolicy().Exists)) return false;
            return PerformanceTweaks.Find(id).Values.All(value =>
                platform.ReadValue(value.Key, value.Name).Same(RegistryValueState.Dword(value.Target)) || platform.CanWriteKey(value.Key));
        }
        catch (Exception ex) when (IsExpectedFailure(ex)) { return false; }
    }

    public Task<OptimizationOutcome> ApplyPerformanceTweakAsync(string id, CancellationToken cancellationToken = default) =>
        ExecuteAsync(_ =>
        {
            var group = PerformanceTweaks.Find(id);
            if (group.RequiresAc && onAcPower() != true) throw new InvalidOperationException("Conecta el equipo a corriente antes de aplicar esta política de energía.");
            if (!CanApplyPerformanceTweak(id)) throw new UnauthorizedAccessException("Windows no permite modificar este ajuste con los permisos actuales.");
            return NewEntry(id, group.Title, group.Values.Select(value => RegistryChange(value.Key, value.Name, RegistryValueState.Dword(value.Target))).ToList());
        }, cancellationToken, () => PerformanceTweaks.Groups.FirstOrDefault(group => group.Id == id)?.RequiresAc != true || onAcPower() == true);

    public Guid? ReadPowerMode()
    {
        try { return platform.GetPowerMode(); }
        catch (Exception ex) when (IsExpectedFailure(ex) || ex is EntryPointNotFoundException) { return null; }
    }

    public bool CanReduceVisualEffects()
    {
        try { foreach (var action in VisualEffects.Actions) _ = platform.GetVisualEffect(action); return true; }
        catch (Exception ex) when (IsExpectedFailure(ex)) { return false; }
    }

    public Task<OptimizationOutcome> SetBestPerformanceModeAsync(Func<bool> onAcPower, CancellationToken cancellationToken = default) =>
        ExecuteAsync(_ => NewEntry("power-mode", "Energía · Máximo rendimiento con corriente",
            [new("PowerMode", "AC", "", RegistryValueState.String(platform.GetPowerMode().ToString()),
                RegistryValueState.String("ded574b5-45a0-4f42-8737-46345c09c238"))]), cancellationToken, onAcPower);

    public Task<OptimizationOutcome> ReduceVisualEffectsAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(_ => NewEntry("visual-effects", "Windows · Reducir efectos visuales",
            VisualEffects.Actions.Select(action => new OptimizationValueChange("VisualEffect", "", action.ToString(System.Globalization.CultureInfo.InvariantCulture),
                RegistryValueState.Dword(platform.GetVisualEffect(action) ? 1 : 0), RegistryValueState.Dword(0))).ToList()), cancellationToken);

    public Task<OptimizationOutcome> SetGameGpuPreferenceAsync(string executablePath, CancellationToken cancellationToken = default) =>
        SetGameGpuPreferenceCheckedAsync(executablePath, null, cancellationToken);

    internal Task<OptimizationOutcome> SetGameGpuPreferenceCheckedAsync(string executablePath, Func<bool>? canApply, CancellationToken cancellationToken) =>
        ExecuteAsync(_ =>
        {
            var path = FortniteGpuPreference.ValidateExecutable(executablePath);
            var previous = platform.ReadValue(OptimizationTargets.GpuPreferences, path);
            var intended = RegistryValueState.String(FortniteGpuPreference.HighPerformanceText(previous));
            return NewEntry("fortnite-gpu", "Fortnite · Preferencia GPU de alto rendimiento",
                [new("Registry", OptimizationTargets.GpuPreferences, path, previous, intended)]);
        }, cancellationToken, canApply);

    public bool? ReadGameGpuPreference(string executablePath)
    {
        try
        {
            var path = FortniteGpuPreference.ValidateExecutable(executablePath);
            return FortniteGpuPreference.IsHighPerformance(platform.ReadValue(OptimizationTargets.GpuPreferences, path));
        }
        catch (Exception ex) when (IsExpectedFailure(ex)) { return null; }
    }

    public Task<OptimizationOutcome> SetStartupEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default) =>
        SetStartupEnabledCoreAsync(id, enabled, null, cancellationToken);

    public Task<OptimizationOutcome> DisableStartupCheckedAsync(string id, string expectedCommand, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(expectedCommand)
            ? Task.FromResult(new OptimizationOutcome(false, "Falta el comando de inicio revisado. Prepara de nuevo la selección."))
            : SetStartupEnabledCoreAsync(id, false, expectedCommand, cancellationToken);

    private Task<OptimizationOutcome> SetStartupEnabledCoreAsync(string id, bool enabled, string? expectedCommand, CancellationToken cancellationToken) =>
        ExecuteAsync(entries =>
        {
            var entry = ReadStartup(entries).FirstOrDefault(x => x.Id == id)
                ?? throw new ArgumentException("La entrada de inicio cambió o ya no está disponible. Actualiza la lista.");
            var current = platform.ReadValue(OptimizationTargets.Run, entry.Name);
            if (expectedCommand is not null && (!PcStartupSelection.CanOffer(entry) || !current.Exists ||
                current.Kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString) ||
                !string.Equals(current.Text, expectedCommand, StringComparison.Ordinal)))
                throw new InvalidOperationException("La entrada de inicio cambió o requiere revisión manual. Se conservó; prepara de nuevo la selección.");
            if (!platform.CanWriteKey(OptimizationTargets.Run)) throw new UnauthorizedAccessException("Windows no permite modificar esta entrada de inicio con los permisos actuales.");
            RegistryValueState target;
            if (!enabled) target = RegistryValueState.Absent;
            else if (current.Exists) target = current;
            else target = FindDisabledValue(entries, entry.Name) ?? throw new InvalidOperationException("No se encontró el respaldo exacto de esta entrada.");
            return NewEntry("startup", "Inicio: " + entry.Name + (enabled ? " (habilitar)" : " (deshabilitar)"),
                [new("Registry", OptimizationTargets.Run, entry.Name, current, target)]);
        }, cancellationToken);

    public Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        OptimizationJournalEntry? entry = null;
        List<OptimizationJournalEntry>? entries = null;
        try
        {
            await using var lease = await journal.LeaseAsync(cancellationToken);
            entries = await journal.ReadAsync(cancellationToken);
            entry = entries.FirstOrDefault(x => x.Id == changeId);
            if (entry is null || entry.State == "Undone") return new OptimizationOutcome(false, "El cambio ya fue restaurado o no existe.");
            foreach (var value in entry.Values)
            {
                var current = ReadCurrent(value);
                if (!current.Same(value.Intended) && !current.Same(value.Previous))
                    return new OptimizationOutcome(false, "Windows u otra aplicación cambió esta preferencia. No se sobrescribió ese cambio.");
                if (value.Kind == "Power" && !platform.ReadPowerSchemes().Any(x => x.Id.Equals(value.Previous.Text, StringComparison.OrdinalIgnoreCase)))
                    return new OptimizationOutcome(false, "El plan de energía anterior ya no existe. Selecciona un plan disponible.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            entry.State = "UndoPending";
            await journal.WriteAsync(entries, cancellationToken);
            var changed = false;
            foreach (var value in entry.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = ReadCurrent(value);
                if (current.Same(value.Previous)) continue;
                if (!current.Same(value.Intended)) throw new InvalidOperationException("La preferencia cambió durante la restauración. Se preservó el cambio externo.");
                WriteCurrent(value, value.Previous);
                changed = true;
                if (!ReadCurrent(value).Same(value.Previous)) throw new IOException("Windows no confirmó la restauración.");
            }
            entry.State = "Undone";
            await journal.WriteAsync(entries, CancellationToken.None);
            return new OptimizationOutcome(true, "Estado anterior restaurado y comprobado.", changed);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            return new OptimizationOutcome(false, FriendlyError(ex) + " El respaldo sigue disponible para recuperación.", entry is not null && HasChangedValue(entry));
        }
    }, cancellationToken);

    private Task<OptimizationOutcome> ExecuteAsync(Func<List<OptimizationJournalEntry>, OptimizationJournalEntry> create,
        CancellationToken token, Func<bool>? canApply = null) => Task.Run(async () =>
    {
        OptimizationJournalEntry? entry = null;
        List<OptimizationJournalEntry>? entries = null;
        try
        {
            await using var lease = await journal.LeaseAsync(token);
            entries = await journal.ReadAsync(token);
            token.ThrowIfCancellationRequested();
            if (canApply?.Invoke() == false) return new OptimizationOutcome(false, "La alimentación cambió o no se confirmó corriente. No se aplicó el cambio.");
            entry = create(entries);
            entry.Values.RemoveAll(x => x.Previous.Same(x.Intended));
            if (entry.Values.Count == 0) return new OptimizationOutcome(true, "La preferencia ya tiene ese estado.");
            foreach (var value in entry.Values) { journal.DemandAuthority(value); value.Previous.Validate(); }
            entries.Add(entry);
            await journal.WriteAsync(entries, token);
            try
            {
                foreach (var value in entry.Values)
                {
                    token.ThrowIfCancellationRequested();
                    if (canApply?.Invoke() == false) throw new InvalidOperationException("La alimentación cambió. Se conservó el estado de Windows.");
                    if (!ReadCurrent(value).Same(value.Previous)) throw new InvalidOperationException("La preferencia cambió antes de aplicarla. Actualiza su estado.");
                    WriteCurrent(value, value.Intended);
                    if (!ReadCurrent(value).Same(value.Intended)) throw new IOException("Windows no confirmó la preferencia aplicada.");
                }
                entry.State = "Completed";
                await journal.WriteAsync(entries, CancellationToken.None);
            }
            catch
            {
                entry.State = "Partial";
                try { await journal.WriteAsync(entries, CancellationToken.None); }
                catch (Exception ex) when (IsExpectedFailure(ex)) {  }
                throw;
            }
            return new OptimizationOutcome(true, "Preferencia aplicada y comprobada. Puedes deshacerla desde los cambios guardados.", true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            return new OptimizationOutcome(false, FriendlyError(ex), entry is not null && HasChangedValue(entry));
        }
    }, token);

    private OptimizationJournalEntry NewEntry(string tool, string title, List<OptimizationValueChange> values) =>
        new() { ToolId = tool, Title = title, AppliedAt = clock(), Values = values };

    private OptimizationValueChange RegistryChange(string key, string name, RegistryValueState intended) =>
        new("Registry", key, name, platform.ReadValue(key, name), intended);

    private RegistryValueState AnimationState() => platform.GetAnimations() is bool enabled
        ? RegistryValueState.Dword(enabled ? 1 : 0) : throw new InvalidOperationException("Windows no permite leer la preferencia de animaciones. No se aplicó ningún cambio.");

    private bool? ReadBool(string key, string name)
    {
        var value = platform.ReadValue(key, name);
        return value.Exists && value.Kind == RegistryValueKind.DWord && value.Number is 0 or 1 ? value.Number == 1 : null;
    }

    private bool? ReadPairedBool(string firstKey, string firstName, string secondKey, string secondName)
    {
        var first = ReadBool(firstKey, firstName);
        var second = ReadBool(secondKey, secondName);
        return first == second ? first : null;
    }

    private static ToggleSetting InspectToggle(string id, string name, string description, Func<bool?> read)
    {
        try
        {
            var enabled = read();
            return new(id, name, description, enabled, enabled switch { true => "Activado", false => "Desactivado", _ => "Sin preferencia explícita o valores mixtos; Windows decide el estado efectivo." });
        }
        catch (Exception ex) when (IsExpectedFailure(ex)) { return new(id, name, description, null, "No se pudo consultar: " + FriendlyError(ex), false, false); }
    }

    private List<StartupEntry> ReadStartup(List<OptimizationJournalEntry> entries)
    {
        var current = platform.ReadRunEntries();
        var writable = platform.CanWriteKey(OptimizationTargets.Run);
        var rows = current.Where(x => x.Value.Exists && x.Value.Kind is RegistryValueKind.String or RegistryValueKind.ExpandString)
            .Select(x => new StartupEntry(StartupId(x.Key), x.Key, x.Value.Text ?? "", true, "Usuario actual · HKCU Run", writable)).ToList();
        var disabled = entries.Where(x => x.ToolId == "startup" && x.State != "Undone").SelectMany(x => x.Values)
            .Where(x => x.Previous.Exists && !x.Intended.Exists).Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var name in disabled)
            if (!current.ContainsKey(name) && FindDisabledValue(entries, name) is { } value)
                rows.Add(new(StartupId(name), name, value.Text ?? "", false, "Usuario actual · HKCU Run · respaldo Silen", writable));
        return rows.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static RegistryValueState? FindDisabledValue(List<OptimizationJournalEntry> entries, string name) =>
        entries.Where(x => x.ToolId == "startup" && x.State != "Undone").Reverse().SelectMany(x => x.Values)
            .FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && x.Previous.Exists && !x.Intended.Exists)?.Previous;

    private static string StartupId(string name) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToUpperInvariant())));

    private RegistryValueState ReadCurrent(OptimizationValueChange value) => value.Kind switch
    {
        "FilterKeys" => RegistryValueState.String(InputPreferences.Encode(platform.GetFilterKeys())),
        "MouseMotion" => RegistryValueState.String(InputPreferences.Encode(platform.GetMouseMotion())),
        "Registry" => platform.ReadValue(value.Key, value.Name),
        "Animations" => AnimationState(),
        "Power" => RegistryValueState.String(platform.GetActivePowerScheme().ToString()),
        "PowerMode" => RegistryValueState.String(platform.GetPowerMode().ToString()),
        "VisualEffect" => RegistryValueState.Dword(platform.GetVisualEffect(VisualEffects.Parse(value.Name)) ? 1 : 0),
        "ServiceStart" => RegistryValueState.Dword((ReadJournalService(value.Key)).StartType),
        "ServiceRunning" => RegistryValueState.Dword((ReadJournalService(value.Key)).Running ? 1 : 0),
        _ => throw new InvalidDataException("Acción de respaldo incompatible.")
    };

    private void WriteCurrent(OptimizationValueChange value, RegistryValueState state)
    {
        journal.DemandAuthority(value);
        switch (value.Kind)
        {
            case "FilterKeys": platform.SetFilterKeys(InputPreferences.Filter(state.Text!)); break;
            case "MouseMotion": platform.SetMouseMotion(InputPreferences.Mouse(state.Text!)); break;
            case "Registry": platform.WriteValue(value.Key, value.Name, state); break;
            case "Animations": platform.SetAnimations(state.Number == 1); break;
            case "Power": platform.SetActivePowerScheme(Guid.Parse(state.Text!)); break;
            case "PowerMode": platform.SetPowerMode(Guid.Parse(state.Text!)); break;
            case "VisualEffect": platform.SetVisualEffect(VisualEffects.Parse(value.Name), state.Number == 1); break;
            case "ServiceStart": if (value.Key == "wuauserv") platform.SetUpdateServiceStartType(checked((int)state.Number)); else platform.SetServiceStartType(value.Key, checked((int)state.Number)); break;
            case "ServiceRunning": if (value.Key == "wuauserv") platform.SetUpdateServiceRunning(state.Number == 1); else platform.SetServiceRunning(value.Key, state.Number == 1); break;
            default: throw new InvalidDataException("Acción de respaldo incompatible.");
        }
    }

    private bool CanUndo(OptimizationJournalEntry entry)
    {
        if (entry.State == "Undone") return false;
        try { return entry.Values.All(x => { var state = ReadCurrent(x); return state.Same(x.Previous) || state.Same(x.Intended); }) && HasChangedValue(entry); }
        catch (Exception ex) when (IsExpectedFailure(ex)) { return false; }
    }

    private bool HasChangedValue(OptimizationJournalEntry entry)
    {
        try { return entry.Values.Any(x => !ReadCurrent(x).Same(x.Previous)); }
        catch (Exception ex) when (IsExpectedFailure(ex)) { return false; }
    }

    private static bool IsExpectedFailure(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException
        or ArgumentException or InvalidOperationException or Win32Exception or EntryPointNotFoundException;

    private static string FriendlyError(Exception ex) => ex is UnauthorizedAccessException or SecurityException
        ? "Windows denegó el acceso. No se pudo completar la preferencia." : ex.Message;
    public Task<TemporaryCleanupPreview> PreviewTempAsync(CancellationToken cancellationToken = default) => cleanup.PreviewAsync(cancellationToken);
    public Task<TemporaryCleanupResult> CleanTempAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) => cleanup.CleanAsync(ids, cancellationToken);
    public Task<IReadOnlyList<CleanupRecoveryItem>> ListRecoveryAsync(CancellationToken cancellationToken = default) => cleanup.ListRecoveryAsync(cancellationToken);
    public Task<TemporaryCleanupResult> RestoreRecoveryAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) => cleanup.RestoreRecoveryAsync(ids, cancellationToken);
    public Task<TemporaryCleanupResult> PurgeRecoveryAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) => cleanup.PurgeRecoveryAsync(ids, cancellationToken);
}
