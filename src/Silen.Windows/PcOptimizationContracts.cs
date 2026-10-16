using Silen.Core;

namespace Silen.Windows;

public sealed record PcOptimizationOption(string Id, string Title, string Description, string Category,
    bool Available, bool SelectedByDefault, bool AlreadyApplied, string Status)
{
    public string ActivationHint => Silen.Core.RestartPlan.ActivationText(Id);
}
public sealed record PcOptimizationPreview(List<PcOptimizationOption> Options);
public sealed record PcOptimizationProgress(int Completed, int Total, string Title);
public sealed record PcOptimizationStep(string Id, string Title, bool Success, bool Changed, bool Skipped, string Message)
{
    public string State => Skipped ? "Omitido" : !Success ? "No se completó" : Changed ? "Aplicado" : "Ya estaba así";
}
public sealed record PcOptimizationResult(List<PcOptimizationStep> Steps, bool Cancelled)
{
    public int Applied => Steps.Count(step => step.Success && step.Changed);
    public int Unchanged => Steps.Count(step => step.Success && !step.Changed && !step.Skipped);
    public int Skipped => Steps.Count(step => step.Skipped);
    public int Failed => Steps.Count(step => !step.Success && !step.Skipped);
    public string Message => $"{(Cancelled ? "Proceso cancelado." : Failed > 0 || Skipped > 0 ? "Proceso completado con pasos pendientes." : "Proceso completado.")} {SpanishText.Count(Applied, "aplicado", "aplicados")} · {Unchanged} sin cambios · {SpanishText.Count(Skipped, "omitido", "omitidos")} · {Failed} con error. Los cambios guardados se pueden restaurar en Ajustes.";
}
public interface IPcOptimizationService
{
    void AttachTimerService(IApplicationTimerService service) { }
    void SetApplicationTarget(string? executablePath) { }
    Task<PcOptimizationPreview> PreviewAsync(CancellationToken cancellationToken = default);
    Task<PcOptimizationResult> ApplyAsync(IReadOnlyCollection<string> ids, IProgress<PcOptimizationProgress>? progress = null, CancellationToken cancellationToken = default);
}
