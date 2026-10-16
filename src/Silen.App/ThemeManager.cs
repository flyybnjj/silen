using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Silen.App;

internal static class ThemeManager
{
    private static bool initialized;
    internal static event Action? ThemeApplied;
    private static string preferred = "Claro";
    internal static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        SystemParameters.StaticPropertyChanged += PreferencesChanged;
        SystemEvents.UserPreferenceChanged += WindowsPreferencesChanged;
        Application.Current.Exit += (_, _) =>
        {
            SystemParameters.StaticPropertyChanged -= PreferencesChanged;
            SystemEvents.UserPreferenceChanged -= WindowsPreferencesChanged;
        };
        Apply(preferred);
    }
    private static void PreferencesChanged(object? sender, PropertyChangedEventArgs args) => Application.Current.Dispatcher.InvokeAsync(() => Apply(preferred));
    private static void WindowsPreferencesChanged(object sender, UserPreferenceChangedEventArgs args) => Application.Current.Dispatcher.InvokeAsync(() => Apply(preferred));
    internal static void Apply(string theme)
        => ApplyCore(theme, SystemParameters.HighContrast);
    internal static void ApplyContrastFixture() => ApplyCore("Claro", true);
    private static void ApplyCore(string theme, bool highContrast)
    {
        preferred = theme is "Oscuro" or "Sistema" ? theme : "Claro";
        var dark = preferred == "Oscuro";
        if (preferred == "Sistema")
        {
            try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0; }
            catch { dark = false; }
        }
        var palette = new Dictionary<string, string>
        {
            ["Ink"] = dark ? "#191A18" : "#F4F2EC", ["Panel"] = dark ? "#21221F" : "#F4F2EC",
            ["Raised"] = dark ? "#2B2C28" : "#EAE8E2", ["Line"] = dark ? "#45473F" : "#CFCEC9",
            ["ControlLine"] = dark ? "#808276" : "#848580", ["Text"] = dark ? "#F0EEE7" : "#292B27",
            ["Muted"] = dark ? "#BCBDB4" : "#595A55", ["Accent"] = dark ? "#EA8B80" : "#B23B35",
            ["Selection"] = dark ? "#363832" : "#E0DED8", ["Hover"] = dark ? "#2E302B" : "#ECEAE4",
            ["ScrollThumb"] = dark ? "#8B8D80" : "#ABAAA5", ["White"] = dark ? "#191A18" : "#F4F2EC"
        };
        var resources = Application.Current.Resources;
        foreach (var item in palette) resources[item.Key] = Brush(item.Value);
        foreach (var key in new[] { "Sidebar", "Chrome" }) resources[key] = resources["Ink"];
        foreach (var key in new[] { "Warning", "ErrorLine", "ErrorText" }) resources[key] = resources["Accent"];
        resources["ErrorSurface"] = resources["Raised"]; resources["MemoryAccent"] = resources["Muted"]; resources["GpuAccent"] = resources["Text"];
        resources["SelectedText"] = resources["Text"];
        if (highContrast)
        {
            foreach (var key in new[] { "Ink", "Panel", "Sidebar", "Chrome" }) resources[key] = SystemColors.WindowBrush;
            foreach (var key in new[] { "Text", "Muted", "Warning", "MemoryAccent", "GpuAccent", "ErrorText" }) resources[key] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "Accent", "ErrorLine" }) resources[key] = SystemColors.HighlightBrush;
            resources["White"] = SystemColors.HighlightTextBrush;
            resources["Selection"] = SystemColors.WindowBrush; resources["SelectedText"] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "Line", "ControlLine", "ScrollThumb" }) resources[key] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "Raised", "Hover", "ErrorSurface" }) resources[key] = SystemColors.WindowBrush;
        }
        // WPF puede congelar el DrawingImage compartido e impedir actualizar su pincel dinámico.
        // Conservamos la geometría original y sustituimos solo la imagen con la tinta del tema.
        if (resources["SilenWordmarkGeometry"] is Geometry geometry)
        {
            var logo = new DrawingImage(new GeometryDrawing((Brush)resources["Text"], null, geometry));
            if (logo.CanFreeze) logo.Freeze();
            resources["SilenLogo"] = logo;
        }
        ThemeApplied?.Invoke();
    }
    private static SolidColorBrush Brush(string value) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); brush.Freeze(); return brush; }
}
