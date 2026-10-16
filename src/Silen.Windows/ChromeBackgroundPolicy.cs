namespace Silen.Windows;

public sealed partial class NativeOptimizationService
{
    private RegistryValueState ReadMachineChromePolicy() => platform.ReadValue(@"HKLM\SOFTWARE\Policies\Google\Chrome", "BackgroundModeEnabled");

    private bool IsChromeRegistered()
    {
        foreach (var key in new[] {
            @"Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
            @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe" })
        {
            var state = platform.ReadValue(key, "");
            if (!state.Exists || state.Kind is not (Microsoft.Win32.RegistryValueKind.String or Microsoft.Win32.RegistryValueKind.ExpandString)) continue;
            var path = (state.Kind == Microsoft.Win32.RegistryValueKind.ExpandString ? Environment.ExpandEnvironmentVariables(state.Text ?? "") : state.Text ?? "").Trim().Trim('"');
            try
            {
                if (Path.GetFileName(path).Equals("chrome.exe", StringComparison.OrdinalIgnoreCase))
                {
                    _ = ApplicationGpuPreference.ValidateExecutable(path);
                    return true;
                }
            }
            catch (Exception ex) when (IsExpectedFailure(ex)) { }
        }
        return false;
    }
}
