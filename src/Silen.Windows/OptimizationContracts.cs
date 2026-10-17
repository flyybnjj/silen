namespace Silen.Windows;

public sealed record ToggleSetting(string Id, string Name, string Description, bool? Enabled, string Status,
    bool CanEnable = true, bool CanDisable = true, string Category = "Windows", bool Recommended = false,
    bool UsesBackupRestore = false);
public sealed record PowerScheme(string Id, string Name, bool Active);
// Enabled indica presencia en HKCU Run, no el estado efectivo de StartupApproved u otras reglas de Windows.
public sealed record StartupEntry(string Id, string Name, string Command, bool Enabled, string Scope, bool CanChange = true);
public sealed record AppliedOptimization(string Id, string Title, DateTimeOffset AppliedAt, bool CanUndo, string Detail, bool Restored = false);
public sealed record OptimizationOverview(List<ToggleSetting> Toggles, List<PowerScheme> PowerSchemes, List<StartupEntry> StartupEntries, List<AppliedOptimization> Changes);
public enum ChangeVerification { Verified, Uncertain }
// Changed solo confirma una diferencia observada. La incertidumbre no equivale a «sin cambios».
public sealed record OptimizationOutcome(bool Success, string Message, bool Changed = false,
    ChangeVerification Verification = ChangeVerification.Verified)
{
    public bool MayHaveChanged => Changed || Verification == ChangeVerification.Uncertain;
}
public sealed record TemporaryFileItem(string Id, string RelativePath, long Bytes);
public sealed record TemporaryCleanupPreview(List<TemporaryFileItem> Files, long TotalBytes, bool Truncated, string Message);
public sealed record TemporaryCleanupResult(int Removed, int Skipped, long BytesFreed, string Message, long RecoverableBytes = 0, string? RecoveryId = null);
// Processed cuenta intentos reales; al cancelar, Skipped también incluye los pendientes conservados.
public sealed record TemporaryCleanupProgress(int Processed, int Total, int Removed, int Skipped, long RecoverableBytes, string Location);
public sealed record CleanupRecoveryItem(string Id, string RelativePath, long Bytes, DateTimeOffset CapturedAt);

public interface INativeOptimizationService
{
    Task<Silen.Core.WindowsUpdateHealth> InspectWindowsUpdateAsync(CancellationToken cancellationToken = default) => Task.FromResult(Silen.Core.WindowsUpdateHealth.Unknown("Estado de Windows Update sin consultar."));
    Task<OptimizationOutcome> ResumeWindowsUpdateAsync(CancellationToken cancellationToken = default) => Task.FromResult(new OptimizationOutcome(false, "Recuperación de Windows Update no disponible."));
    Task<OptimizationOutcome> SetApplicationGpuPreferenceAsync(string executablePath, CancellationToken cancellationToken = default) => Task.FromResult(new OptimizationOutcome(false, "Preferencia GPU no disponible."));
    bool? ReadApplicationGpuPreference(string executablePath) => null;
    Task<OptimizationOutcome> DisableStartupCheckedAsync(string id, string expectedCommand, CancellationToken cancellationToken = default) => Task.FromResult(new OptimizationOutcome(false, "La desactivación revisada de inicio no está disponible."));
    Task<OptimizationOverview> InspectAsync(CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> SetToggleAsync(string id, bool enabled, CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> SetPowerSchemeAsync(string id, CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> SetStartupEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default);
    Task<TemporaryCleanupPreview> PreviewTempAsync(CancellationToken cancellationToken = default);
    Task<TemporaryCleanupResult> CleanTempAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);
    Task<TemporaryCleanupResult> CleanTempAsync(IReadOnlyCollection<string> ids, IProgress<TemporaryCleanupProgress>? progress, CancellationToken cancellationToken = default) => CleanTempAsync(ids, cancellationToken);
    Task<IReadOnlyList<CleanupRecoveryItem>> ListRecoveryAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CleanupRecoveryItem>>([]);
    Task<TemporaryCleanupResult> RestoreRecoveryAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) => Task.FromResult(new TemporaryCleanupResult(0, ids.Count, 0, "Recuperación de temporales no disponible."));
    Task<TemporaryCleanupResult> PurgeRecoveryAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) => Task.FromResult(new TemporaryCleanupResult(0, ids.Count, 0, "Purga de temporales no disponible."));
}
