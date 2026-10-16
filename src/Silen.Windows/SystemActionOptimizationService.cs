using Silen.Core;
namespace Silen.Windows;

/// <summary>La interfaz y Bloom usan este servicio sin privilegios; únicamente el catálogo de equipo cruza UAC.</summary>
public sealed class SystemActionOptimizationService : INativeOptimizationService
{
    public NativeOptimizationService Local { get; }
    private readonly Func<SystemActionRequest, CancellationToken, Task<SystemActionResponse>> execute;
    private OptimizationOverview? systemOverview;
    public static SystemActionOptimizationService CreateDefault(Func<SystemActionRequest, CancellationToken, Task<SystemActionResponse>> execute) => new(new NativeOptimizationService(), execute);
    public SystemActionOptimizationService(NativeOptimizationService local, Func<SystemActionRequest, CancellationToken, Task<SystemActionResponse>> execute)
    { Local = local; this.execute = execute; }
    public async Task<OptimizationOverview> InspectAsync(CancellationToken cancellationToken = default)
    {
        var local = await Local.InspectAsync(cancellationToken);
        var snapshot = systemOverview;
        var toggles = local.Toggles.Select(toggle =>
        {
            if (!SystemActionPolicy.IsPrivilegedToggle(toggle.Id)) return toggle;
            var stored = snapshot?.Toggles.FirstOrDefault(item => item.Id == toggle.Id);
            var available = toggle.Id.StartsWith("service-", StringComparison.Ordinal) ? toggle.Enabled is not null :
                toggle.Id != "power-throttling" || WindowsPowerStatus.ReadAc() == true || toggle.Enabled == true;
            return toggle with { CanEnable = available, CanDisable = stored?.CanDisable == true,
                Status = available ? toggle.Status + " · requiere confirmar UAC para esta operación" : toggle.Status };
        }).ToList();
        var changes = local.Changes.Concat(snapshot?.Changes.Select(change => change with { Id = "system:" + change.Id }) ?? [])
            .OrderByDescending(change => change.AppliedAt).ToList();
        return local with { Toggles = toggles, Changes = changes };
    }
    private async Task<OptimizationOutcome> ExecuteAsync(SystemActionRequest request, CancellationToken token)
    {
        SystemActionPolicy.Validate(request);
        var result = await execute(request, token);
        if (result.Overview is { } overview)
            systemOverview = overview with { Toggles = overview.Toggles.Where(t => SystemActionPolicy.IsPrivilegedToggle(t.Id)).ToList(), StartupEntries = [], PowerSchemes = [] };
        return result.Outcome;
    }
    public Task<OptimizationOutcome> RefreshSystemHistoryAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(new(SystemActionKind.InspectHistory), cancellationToken);
    public Task<OptimizationOutcome> SetToggleAsync(string id, bool enabled, CancellationToken cancellationToken = default) =>
        SystemActionPolicy.IsPrivilegedToggle(id) ? ExecuteAsync(new(SystemActionKind.SetToggle, id, enabled), cancellationToken) : Local.SetToggleAsync(id, enabled, cancellationToken);
    public Task<OptimizationOutcome> ResumeWindowsUpdateAsync(CancellationToken cancellationToken = default) => ExecuteAsync(new(SystemActionKind.ResumeWindowsUpdate), cancellationToken);
    public Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default) => changeId.StartsWith("system:", StringComparison.Ordinal)
        ? ExecuteAsync(new(SystemActionKind.Undo, changeId[7..]), cancellationToken) : Local.UndoAsync(changeId, cancellationToken);
    public Task<WindowsUpdateHealth> InspectWindowsUpdateAsync(CancellationToken cancellationToken = default) => Local.InspectWindowsUpdateAsync(cancellationToken);
    public Task<OptimizationOutcome> SetApplicationGpuPreferenceAsync(string executablePath, CancellationToken cancellationToken = default) => Local.SetApplicationGpuPreferenceAsync(executablePath, cancellationToken);
    public bool? ReadApplicationGpuPreference(string executablePath) => Local.ReadApplicationGpuPreference(executablePath);
    public Task<OptimizationOutcome> DisableStartupCheckedAsync(string id, string expectedCommand, CancellationToken cancellationToken = default) => Local.DisableStartupCheckedAsync(id, expectedCommand, cancellationToken);
    public Task<OptimizationOutcome> SetPowerSchemeAsync(string id, CancellationToken cancellationToken = default) => Local.SetPowerSchemeAsync(id, cancellationToken);
    public Task<OptimizationOutcome> SetStartupEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default) => Local.SetStartupEnabledAsync(id, enabled, cancellationToken);
    public Task<TemporaryCleanupPreview> PreviewTempAsync(CancellationToken cancellationToken = default) => Local.PreviewTempAsync(cancellationToken);
    public Task<TemporaryCleanupResult> CleanTempAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) => Local.CleanTempAsync(ids, cancellationToken);
    public Task<IReadOnlyList<CleanupRecoveryItem>> ListRecoveryAsync(CancellationToken cancellationToken = default) => Local.ListRecoveryAsync(cancellationToken);
    public Task<TemporaryCleanupResult> RestoreRecoveryAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) => Local.RestoreRecoveryAsync(ids, cancellationToken);
    public Task<TemporaryCleanupResult> PurgeRecoveryAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) => Local.PurgeRecoveryAsync(ids, cancellationToken);
}
