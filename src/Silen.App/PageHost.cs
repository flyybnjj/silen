using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Silen.App;

// La navegación visible pertenece a la lista lateral. Este contenedor sin
// cabeceras expone el contenido elegido como panel, no como pestañas invisibles.
public sealed class PageHost : TabControl
{
    public PageHost() { Focusable = false; IsTabStop = false; }

    protected override AutomationPeer OnCreateAutomationPeer() => new PageHostAutomationPeer(this);

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        UIElementAutomationPeer.FromElement(this)?.InvalidatePeer();
    }

    private sealed class PageHostAutomationPeer(PageHost owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override string GetClassNameCore() => nameof(PageHost);
        protected override string GetNameCore() => owner.SelectedItem is TabItem item ? $"Contenido de {item.Header}" : "Contenido de la vista";
    }
}
