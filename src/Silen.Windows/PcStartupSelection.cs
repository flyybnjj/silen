namespace Silen.Windows;

internal static class PcStartupSelection
{
    private static readonly string[] ProtectedMarkers = [
        "securityhealth", "windows security", "defender", "antimalware", "antivirus",
        "avast", "avgui", "kaspersky", "bitdefender", "bdagent", "eset", "egui.exe",
        "mcafee", "norton", "symantec", "sophos", "malwarebytes", "mbam", "crowdstrike",
        "falcon", "sentinel", "trend micro", "trendmicro", "easyanticheat", "easy anti-cheat",
        "battleye", "beservice", "vanguard", "vgtray", "faceit", "ricochet", "anticheat", "anti-cheat",
        "realtek", "rtkaud", "nahimic", "hdaudio", "igfx", "intel graphics", "nvidia", "amd radeon",
        @"\windows\system32\", @"\windows\syswow64\", "%systemroot%", "%windir%"
    ];

    internal static bool CanOffer(StartupEntry entry) => entry.Enabled && !string.IsNullOrWhiteSpace(entry.Command) &&
        !ProtectedMarkers.Any(marker => (entry.Name + " " + entry.Command).Contains(marker, StringComparison.OrdinalIgnoreCase));
}
