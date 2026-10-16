using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Silen.App;

public partial class SummaryErrorNotice : UserControl
{
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string), typeof(SummaryErrorNotice), new PropertyMetadata(""));
    public static readonly DependencyProperty DismissCommandProperty = DependencyProperty.Register(nameof(DismissCommand), typeof(ICommand), typeof(SummaryErrorNotice));
    public static readonly DependencyProperty ShowMarkProperty = DependencyProperty.Register(nameof(ShowMark), typeof(bool), typeof(SummaryErrorNotice), new PropertyMetadata(false));
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public ICommand? DismissCommand { get => (ICommand?)GetValue(DismissCommandProperty); set => SetValue(DismissCommandProperty, value); }
    public bool ShowMark { get => (bool)GetValue(ShowMarkProperty); set => SetValue(ShowMarkProperty, value); }

    public SummaryErrorNotice()
    {
        InitializeComponent();
        RefreshPalette();
        Loaded += (_, _) => { ThemeManager.ThemeApplied += RefreshPalette; RefreshPalette(); };
        Unloaded += (_, _) => ThemeManager.ThemeApplied -= RefreshPalette;
    }

    private void RefreshPalette()
    {
        // Alias locales: las otras vistas conservan sus recursos y su tratamiento actual.
        Resources["ErrorText"] = Application.Current.Resources["Text"];
        Resources["ErrorLine"] = Application.Current.Resources["ControlLine"];
        Resources["ErrorSurface"] = Application.Current.Resources["Raised"];
    }
}
