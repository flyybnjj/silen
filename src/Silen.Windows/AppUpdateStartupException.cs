namespace Silen.Windows;

// Si el proceso nuevo sigue vivo, no se sobrescriben sus archivos ni se elimina
// la copia anterior. Si terminó, el instalador puede restaurar la versión previa.
internal sealed class AppUpdateStartupException(string message, bool processMayBeRunning, Exception? inner = null)
    : IOException(message, inner)
{
    internal bool ProcessMayBeRunning { get; } = processMayBeRunning;
}
