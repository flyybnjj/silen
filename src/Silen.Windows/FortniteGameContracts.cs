namespace Silen.Windows;

public sealed record FortniteInstallation(bool Installed, string? InstallDirectory, string? ExecutablePath,
    string? SettingsPath, bool IsRunning, string Message);

public interface IFortniteGameService
{
    Task<FortniteInstallation> DetectAsync(string? installDirectory = null, CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> ApplyCompetitiveSettingsAsync(CancellationToken cancellationToken = default);
    Task<List<AppliedOptimization>> ReadChangesAsync(CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default);
}
