using System.Diagnostics;
using Silen.Core;

namespace Silen.Windows;

public static class WindowsActions
{
    public static void OpenTrustedAiPage(string url)
    {
        if (url is not ("https://aistudio.google.com/app/apikey" or "https://platform.openai.com/api-keys" or "https://platform.claude.com/settings/keys"))
            throw new ArgumentException("Página de claves no permitida.", nameof(url));
        using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    private static readonly Dictionary<string, string> ManufacturerSites = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NVIDIA"] = "https://www.nvidia.com/en-us/drivers/",
        ["NVIDIA Corporation"] = "https://www.nvidia.com/en-us/drivers/",
        ["AMD"] = "https://www.amd.com/en/support/download/drivers.html",
        ["Advanced Micro Devices, Inc."] = "https://www.amd.com/en/support/download/drivers.html",
        ["Advanced Micro Devices, Inc"] = "https://www.amd.com/en/support/download/drivers.html",
        ["Intel"] = "https://www.intel.com/content/www/us/en/download-center/home.html",
        ["Intel Corporation"] = "https://www.intel.com/content/www/us/en/download-center/home.html",
        ["Dell"] = "https://www.dell.com/support/home",
        ["Dell Inc."] = "https://www.dell.com/support/home",
        ["Dell Inc"] = "https://www.dell.com/support/home",
        ["HP"] = "https://support.hp.com/drivers",
        ["Hewlett-Packard"] = "https://support.hp.com/drivers",
        ["HP Inc."] = "https://support.hp.com/drivers",
        ["Lenovo"] = "https://pcsupport.lenovo.com/",
        ["ASUS"] = "https://www.asus.com/support/download-center/",
        ["ASUSTeK COMPUTER INC."] = "https://www.asus.com/support/download-center/",
        ["Acer"] = "https://www.acer.com/support/drivers-and-manuals",
        ["MSI"] = "https://www.msi.com/support",
        ["Micro-Star International Co., Ltd."] = "https://www.msi.com/support",
        ["Microsoft"] = "https://www.catalog.update.microsoft.com/",
        ["Microsoft Corporation"] = "https://www.catalog.update.microsoft.com/"
    };

    private static readonly HashSet<string> ApprovedHosts = ManufacturerSites.Values
        .Select(value => new Uri(value).Host).Concat(["download.lenovo.com", "support.lenovo.com", "support.microsoft.com"])
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string? ManufacturerUrl(string manufacturer) =>
        ManufacturerSites.GetValueOrDefault(manufacturer?.Trim() ?? "");

    public static void Open(ActionKind action, string? url = null)
    {
        if (action == ActionKind.None && url is null) return;
        using var process = Process.Start(CreateStartInfo(action, url));
    }

    internal static ProcessStartInfo CreateStartInfo(ActionKind action, string? url = null)
    {
        if (action == ActionKind.OpenManufacturer)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || !ApprovedHosts.Contains(uri.IdnHost))
                throw new ArgumentException("El enlace debe ser HTTPS y pertenecer a un fabricante aprobado.", nameof(url));
            return new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true };
        }

        if (url is not null)
            throw new ArgumentException("Esta acción no admite un enlace personalizado.", nameof(url));

        if (action == ActionKind.OpenTaskManager)
            return new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "Taskmgr.exe")) { UseShellExecute = false };

        var target = action switch
        {
            ActionKind.OpenStartup => "ms-settings:startupapps",
            ActionKind.OpenStorage => "ms-settings:storagesense",
            ActionKind.OpenPower => "ms-settings:powersleep",
            ActionKind.OpenWindowsUpdate => "ms-settings:windowsupdate",
            ActionKind.OpenOptionalDrivers => "ms-settings:windowsupdate-optionalupdates",
            ActionKind.OpenGraphics => "ms-settings:display-advancedgraphics",
            ActionKind.OpenGameMode => "ms-settings:gaming-gamemode",
            _ => throw new ArgumentOutOfRangeException(nameof(action), "Acción no permitida.")
        };
        return new ProcessStartInfo(target) { UseShellExecute = true };
    }
}
