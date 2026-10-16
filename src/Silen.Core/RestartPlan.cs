namespace Silen.Core;

public enum ChangeActivation { Immediate, ReopenApplication, NextSignIn, RestartRecommended }

/// <summary>Preferencia de esta sesión; nunca programa un reinicio por detectar cambios.</summary>
public sealed class RestartPlan
{
    public int AppliedCount { get; private set; }
    public int PartialCount { get; private set; }
    public bool HasChanges => AppliedCount > 0 || PartialCount > 0;
    public void RegisterPartial(IEnumerable<string> ids)
    {
        foreach (var id in ids) { PartialCount++; RestartRecommended |= Activation(id) == ChangeActivation.RestartRecommended; }
    }
    public bool RestartRecommended { get; private set; }
    public bool OnClose { get; private set; }
    public void Register(IEnumerable<string> appliedIds)
    {
        foreach (var id in appliedIds) { AppliedCount++; RestartRecommended |= Activation(id) == ChangeActivation.RestartRecommended; }
    }
    public void ScheduleOnClose() { if (HasChanges) OnClose = true; }
    public void Cancel() => OnClose = false;
    public bool TakeDeferredRestart() { var scheduled = OnClose; OnClose = false; return scheduled; }
    public static ChangeActivation Activation(string id) => id switch
    {
        "power-throttling" or "gaming-network-bandwidth" => ChangeActivation.RestartRecommended,
        "application-gpu" or "edge-background" or "chrome-background" or "game-mode" or "gamebar-background" or "xbox-captures" => ChangeActivation.ReopenApplication,
        "startup" => ChangeActivation.NextSignIn,
        _ when id.StartsWith("startup:", StringComparison.Ordinal) => ChangeActivation.NextSignIn,
        _ => ChangeActivation.Immediate
    };
    public static string ActivationText(string id) => Activation(id) switch
    {
        ChangeActivation.ReopenApplication => "Vuelve a abrir la aplicación afectada.",
        ChangeActivation.NextSignIn => "Se refleja en el próximo inicio de sesión.",
        ChangeActivation.RestartRecommended => "Se recomienda reiniciar y comprobar el estado después.",
        _ => "Se solicita a Windows durante esta operación."
    };
}
