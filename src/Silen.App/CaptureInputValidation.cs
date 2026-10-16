using System.IO;

namespace Silen.App;


public static class CaptureInputValidation
{
    public static string Validate(string processId, string seconds, string executable, Func<string, bool> fileExists, out int pid, out int duration)
    {
        pid = 0; duration = 0;
        if (!int.TryParse(processId, out pid) || pid <= 0)
            return "Elige un proceso de la lista o escribe un PID mayor que cero. Puedes buscarlo en el Administrador de tareas.";
        if (!int.TryParse(seconds, out duration) || duration is < 15 or > 300)
            return "Escribe una duración entera entre 15 y 300 segundos.";
        if (string.IsNullOrWhiteSpace(executable) || !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase) || !fileExists(executable))
            return "Selecciona un archivo .exe existente de PresentMon de consola desde su fuente oficial.";
        return "";
    }
}
