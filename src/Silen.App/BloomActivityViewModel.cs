namespace Silen.App;

/// <summary>Mensajes locales de operaciones reales. No utiliza una IA ni envía datos del equipo.</summary>
public sealed class BloomActivityViewModel : ObservableObject
{
    private bool visible, working, detailed;
    private string title = "", detail = "";
    public bool IsVisible => visible;
    public bool IsWorking => working;
    public string Title => title;
    public string Detail => detail;
    internal void Begin(string operation)
    {
        visible = working = true; detailed = false; detail = "Puedes cancelar. Esperaré a que termine el paso en curso.";
        title = operation switch
        {
            var text when text.Contains("temporales", StringComparison.OrdinalIgnoreCase) => "Estoy revisando tus temporales.",
            var text when text.Contains("ajustes del PC", StringComparison.OrdinalIgnoreCase) => "Estoy preparando las opciones.",
            var text when text.Contains("Optimizando", StringComparison.OrdinalIgnoreCase) || text.Contains("selección manual", StringComparison.OrdinalIgnoreCase) => "Estoy aplicando tu selección.",
            var text when text.Contains("análisis", StringComparison.OrdinalIgnoreCase) => "Estoy analizando tu equipo.",
            _ => "Estoy atendiendo tu solicitud."
        };
        Changed(null);
    }
    internal void Report(string heading, string description)
    {
        if (!working) return;
        title = heading; detail = description; Changed(null);
    }
    internal void DetailResult(string heading, string description)
    {
        if (!working) return;
        title = heading; detail = description; detailed = true; Changed(null);
    }
    internal void Finish(bool completed, bool cancelled)
    {
        working = false;
        if (!completed || cancelled)
        {
            title = cancelled ? "He detenido la operación." : "No pude completar la operación.";
            if (!detailed) detail = cancelled ? "Revisa el resultado antes de volver a intentarlo." : "El aviso de esta vista explica lo que ocurrió.";
        }
        else if (!detailed) { title = "He terminado la revisión."; detail = "El resultado está disponible en esta vista."; }
        Changed(null);
    }
    internal void Dismiss() { if (working) return; visible = false; Changed(null); }
}
