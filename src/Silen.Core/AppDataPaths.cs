namespace Silen.Core;

public static class AppDataPaths
{
    public static string Root { get; } = ResolveRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    internal static string ResolveRoot(string localAppData)
    {
        // Share existing history, rollback backups and leases with older installations.
        var legacy = Path.Combine(localAppData, "FlyyOptimizer");
        return Directory.Exists(legacy) ? legacy : Path.Combine(localAppData, "Silen");
    }
}
