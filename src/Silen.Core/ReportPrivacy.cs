namespace Silen.Core;

public static class ReportPrivacy
{
    // Los comandos originales pertenecen al respaldo local, nunca al diagnóstico compartible.
    public static SystemSnapshot ForSharing(SystemSnapshot snapshot) => snapshot with
    {
        Startup = snapshot.Startup.Select(entry => entry with { Command = "[comando local omitido]" }).ToList()
    };
}
