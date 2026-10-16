using Silen.Core;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class WindowsActionsTests
{
    [Theory]
    [InlineData("NVIDIA Corporation", "www.nvidia.com")]
    [InlineData("Advanced Micro Devices, Inc.", "www.amd.com")]
    [InlineData("Intel Corporation", "www.intel.com")]
    [InlineData("Dell Inc.", "www.dell.com")]
    [InlineData("HP", "support.hp.com")]
    [InlineData("LENOVO", "pcsupport.lenovo.com")]
    [InlineData("ASUSTeK COMPUTER INC.", "www.asus.com")]
    [InlineData("Acer", "www.acer.com")]
    [InlineData("Micro-Star International Co., Ltd.", "www.msi.com")]
    [InlineData("Microsoft Corporation", "www.catalog.update.microsoft.com")]
    public void Known_manufacturers_return_official_https_sites(string manufacturer, string host)
    {
        var url = new Uri(WindowsActions.ManufacturerUrl(manufacturer)!);
        Assert.Equal("https", url.Scheme);
        Assert.Equal(host, url.Host);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown NVIDIA partner")]
    [InlineData("Dell Inc. attacker")]
    public void Unknown_manufacturers_do_not_guess_websites(string manufacturer) =>
        Assert.Null(WindowsActions.ManufacturerUrl(manufacturer));

    [Theory]
    [InlineData("http://www.nvidia.com/en-us/drivers/")]
    [InlineData("https://www.nvidia.com.attacker.example/drivers")]
    [InlineData("https://attacker.example/www.nvidia.com")]
    [InlineData("https://attacker@www.nvidia.com/drivers")]
    [InlineData("https://www.nvidia.com:8443/drivers")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("cmd.exe /c whoami")]
    [InlineData("")]
    public void Manufacturer_action_rejects_unapproved_targets(string url) =>
        Assert.Throws<ArgumentException>(() => WindowsActions.CreateStartInfo(ActionKind.OpenManufacturer, url));

    [Fact]
    public void Approved_manufacturer_action_uses_validated_url()
    {
        var start = WindowsActions.CreateStartInfo(ActionKind.OpenManufacturer, "https://www.nvidia.com/en-us/drivers/");
        Assert.Equal("https://www.nvidia.com/en-us/drivers/", start.FileName);
        Assert.Empty(start.Arguments);
    }

    [Theory]
    [InlineData(ActionKind.OpenStartup, "ms-settings:startupapps")]
    [InlineData(ActionKind.OpenStorage, "ms-settings:storagesense")]
    [InlineData(ActionKind.OpenPower, "ms-settings:powersleep")]
    [InlineData(ActionKind.OpenWindowsUpdate, "ms-settings:windowsupdate")]
    [InlineData(ActionKind.OpenOptionalDrivers, "ms-settings:windowsupdate-optionalupdates")]
    [InlineData(ActionKind.OpenGraphics, "ms-settings:display-advancedgraphics")]
    [InlineData(ActionKind.OpenGameMode, "ms-settings:gaming-gamemode")]
    public void Settings_actions_have_fixed_targets(ActionKind action, string expected)
    {
        var start = WindowsActions.CreateStartInfo(action);
        Assert.Equal(expected, start.FileName);
        Assert.Empty(start.Arguments);
    }

    [Fact]
    public void Task_manager_uses_the_windows_system_directory()
    {
        var start = WindowsActions.CreateStartInfo(ActionKind.OpenTaskManager);
        Assert.Equal(Path.Combine(Environment.SystemDirectory, "Taskmgr.exe"), start.FileName);
        Assert.False(start.UseShellExecute);
    }

    [Fact]
    public void A_url_cannot_replace_a_settings_target() =>
        Assert.Throws<ArgumentException>(() => WindowsActions.CreateStartInfo(ActionKind.OpenStorage, "https://attacker.example"));

    [Fact]
    public void Undefined_actions_are_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowsActions.CreateStartInfo((ActionKind)999));

    [Fact]
    public void Microsoft_inbox_drivers_are_not_routed_to_surface_support() =>
        Assert.DoesNotContain("surface", WindowsActions.ManufacturerUrl("Microsoft Corporation")!, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Official_lenovo_downloads_open_as_https_links_without_installer_arguments()
    {
        var start = WindowsActions.CreateStartInfo(ActionKind.OpenManufacturer, "https://download.lenovo.com/pccbbs/mobiles/driver.exe");
        Assert.Equal("https://download.lenovo.com/pccbbs/mobiles/driver.exe", start.FileName);
        Assert.True(start.UseShellExecute);
        Assert.Empty(start.Arguments);
    }
}
