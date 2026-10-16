namespace Silen.Windows;

public sealed record FortniteProfileOption(string Id, string Title, string Description, bool Available, bool SelectedByDefault, string Status);
public sealed record FortniteProfilePreview(FortniteInstallation Installation, List<FortniteProfileOption> Options, List<AppliedOptimization> Changes, string Message);
public sealed record FortniteProfileProgress(int Completed, int Total, string Title);
public sealed record FortniteProfileStepResult(string Id, string Title, bool Success, bool Changed, string Message);
public sealed record FortniteProfileResult(List<FortniteProfileStepResult> Steps, bool Cancelled, string Message);

public interface IFortniteProfileService
{
    Task<FortniteProfilePreview> PreviewAsync(string? installDirectory = null, CancellationToken cancellationToken = default);
    Task<FortniteProfileResult> ApplyAsync(IReadOnlyCollection<string> optionIds, IProgress<FortniteProfileProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<OptimizationOutcome> UndoAsync(string changeId, CancellationToken cancellationToken = default);
}
