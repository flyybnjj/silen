using System.Windows.Input;
using Silen.Core;

namespace Silen.App;

public sealed class RestartViewModel : ObservableObject
{
    private readonly RestartPlan plan = new();
    private readonly Action restart;
    private readonly Func<string, string, bool> confirm;
    private string error = "";
    public RestartViewModel(Action restart, Func<string, string, bool> confirm, Func<bool> canRun)
    {
        this.restart = restart; this.confirm = confirm;
        RestartNowCommand = new RelayCommand(_ => RestartNow(), _ => canRun() && HasChanges);
        RestartOnCloseCommand = new RelayCommand(_ => { plan.ScheduleOnClose(); Changed(null); }, _ => canRun() && HasChanges && !OnClose);
        CancelCommand = new RelayCommand(_ => { plan.Cancel(); Changed(null); }, _ => OnClose);
    }
    public bool HasChanges => plan.HasChanges;
    public bool OnClose => plan.OnClose;
    private string ChangeSummary => SpanishText.Count(plan.AppliedCount, "cambio completado", "cambios completados") + (plan.PartialCount > 0 ? $"; {SpanishText.Count(plan.PartialCount, "cambio parcial", "cambios parciales")}. Revisa los respaldos: no se confirmó su aplicación completa ni su beneficio. " : ". ");
    public string Status => error.Length > 0 ? error : ChangeSummary + (OnClose
        ? "Elegiste reiniciar Windows al cerrar Silen. Puedes cancelar esta elección. Guarda tu trabajo antes de salir."
        : plan.RestartRecommended ? "Se recomienda reiniciar y comprobar el resultado." : "No todos necesitan reinicio. Algunos se reflejan al volver a abrir la aplicación o iniciar sesión.");
    public ICommand RestartNowCommand { get; }
    public ICommand RestartOnCloseCommand { get; }
    public ICommand CancelCommand { get; }
    internal void Register(IEnumerable<string> ids) { plan.Register(ids); error = ""; Changed(null); CommandManager.InvalidateRequerySuggested(); }
    internal void RegisterPartial(IEnumerable<string> ids) { plan.RegisterPartial(ids); error = ""; Changed(null); CommandManager.InvalidateRequerySuggested(); }
    private void RestartNow()
    {
        if (!confirm("Reiniciar Windows", "Guarda tu trabajo. ¿Quieres reiniciar el PC ahora? Silen no forzará el cierre de otras aplicaciones.")) return;
        plan.Cancel(); Request();
    }
    internal void FinishClose(bool isApplicationUpdate)
    {
        if (isApplicationUpdate || !plan.TakeDeferredRestart()) return;
        if (confirm("Reinicio al cerrar", "Elegiste reiniciar al cerrar Silen. ¿Reiniciar Windows ahora? Guarda tu trabajo; no se forzará el cierre de otras aplicaciones.")) Request();
    }
    private void Request()
    {
        try { restart(); error = "Solicitud de reinicio enviada a Windows. Otras aplicaciones pueden impedirla para proteger tu trabajo."; }
        catch { error = "Windows no aceptó el reinicio. Guarda tu trabajo y reinicia desde el menú Inicio."; }
        Changed(null);
    }
}
