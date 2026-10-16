using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Silen.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel model;
    private readonly RibbonAnimator ribbonAnimator;
    private readonly DispatcherTimer ramTimer;
    private bool closingRequested;
    private bool readyToClose;
    public MainWindow(MainViewModel model, bool animateStartup = false)
    {
        this.model = model;
        ThemeManager.Initialize();
        InitializeComponent();
        DataContext = model;
        model.Bloom.Messages.CollectionChanged += BloomMessagesChanged;
        ribbonAnimator = new(this, model, RibbonArtwork, ApplicationChrome, ReadabilityVeil, animateStartup);
        ramTimer = new(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) =>
        {
            if (!IsVisible || !IsActive || WindowState == WindowState.Minimized) return;
            model.PcOptimization.RefreshTimerStatus(); if (model.SelectedPage == 3) model.Settings.RefreshTimerProfiles();
            if (IsActive && model.SelectedPage == 1 && model.CanStartOperation) model.RamNetwork.UpdateLive();
        }, Dispatcher);
        Closing += HandleClosing;
        model.SectionNavigationRequested += NavigateToSection;
        model.AppUpdate.RestartRequested += RestartWithUpdate;
        Closed += (_, _) => { ramTimer.Stop(); ribbonAnimator.Dispose(); model.Bloom.Messages.CollectionChanged -= BloomMessagesChanged; model.SectionNavigationRequested -= NavigateToSection; model.AppUpdate.RestartRequested -= RestartWithUpdate; };
    }

    internal void CaptureMotionAt(double seconds) => ribbonAnimator.CaptureAt(seconds);
    internal void CaptureArtworkAt(double phase) => ribbonAnimator.CaptureArtworkAt(phase);
    internal RibbonAnimator MotionDiagnostics => ribbonAnimator;

    private void BloomMessagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args) =>
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => BloomChatScroll.ScrollToEnd()));

    private async void SaveBloomKey(object sender, RoutedEventArgs args)
    {
        var key = BloomPassword.Password;
        BloomPassword.Clear();
        await model.Bloom.SaveKeyAsync(key);
    }

    private void RestartWithUpdate()
    {
        if (!closingRequested && model.TryStartAppUpdate()) Close();
    }

    private void NavigateToSection(string name)
    {
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (closingRequested || FindName(name) is not FrameworkElement section) return;
            if (section is Expander targetExpander) targetExpander.IsExpanded = true;
            for (DependencyObject? current = section; current is not null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
                if (current is Expander expander) expander.IsExpanded = true;
            UpdateLayout();
            if (!section.IsVisible) return;
            section.BringIntoView(new Rect(0, 0, Math.Max(1, section.ActualWidth), Math.Min(160, Math.Max(1, section.ActualHeight))));
            if (IsActive && section is Panel panel && panel.Children.OfType<Control>().FirstOrDefault(c => c.Focusable && c.IsEnabled) is { } control)
                control.Focus();
        }));
    }

    private async void HandleClosing(object? sender, CancelEventArgs args)
    {
        if (readyToClose) return;
        args.Cancel = true;
        if (closingRequested) return;
        closingRequested = true;
        ribbonAnimator.Dispose();
        IsEnabled = false;
        await model.PrepareToCloseAsync();
        model.Restart.FinishClose(model.AppUpdate.IsRestarting);
        readyToClose = true;

        _ = Dispatcher.BeginInvoke(new Action(Close));
    }
}
