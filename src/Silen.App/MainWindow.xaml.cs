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
        if (animateStartup && !model.HasSnapshot) model.BeginStartupPresentation();
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
        PreviewKeyDown += HandleDialogKey;
        model.SectionNavigationRequested += NavigateToSection;
        model.PropertyChanged += PresentSelectionDialog;
        model.AppUpdate.RestartRequested += RestartWithUpdate;
        Closed += (_, _) => { ramTimer.Stop(); ribbonAnimator.Dispose(); model.Bloom.Messages.CollectionChanged -= BloomMessagesChanged; model.SectionNavigationRequested -= NavigateToSection; model.PropertyChanged -= PresentSelectionDialog; model.AppUpdate.RestartRequested -= RestartWithUpdate; };
    }

    internal void CaptureMotionAt(double seconds) => ribbonAnimator.CaptureAt(seconds);
    internal void CaptureArtworkAt(double phase) => ribbonAnimator.CaptureArtworkAt(phase);
    internal RibbonAnimator MotionDiagnostics => ribbonAnimator;

    private void HandleDialogKey(object sender, System.Windows.Input.KeyEventArgs args)
    {
        if (args.Key != System.Windows.Input.Key.Escape || !model.IsOptimizationDialogOpen) return;
        if (model.IsBusy && model.CancelCommand.CanExecute(null)) { model.CancelCommand.Execute(null); args.Handled = true; return; }
        if (model.CloseOptimizationDialogCommand.CanExecute(null)) model.CloseOptimizationDialogCommand.Execute(null);
        args.Handled = true;
    }

    private void PresentSelectionDialog(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(MainViewModel.IsOptimizationDialogOpen) or nameof(MainViewModel.IsBusy))) return;
        if (!model.IsOptimizationDialogOpen || !IsActive) return;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (closingRequested || !model.IsOptimizationDialogOpen || OptimizationDialog.IsKeyboardFocusWithin) return;
            UpdateLayout(); FirstAction(OptimizationDialog)?.Focus();
        }));
    }

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
            if (IsActive && FirstAction(section) is { } control)
                control.Focus();
            // El cambio de foco puede desplazar la lista; conservar después el
            // encabezado y la primera opción, no enfocar el contenedor completo.
            section.BringIntoView(new Rect(0, 0, Math.Max(1, section.ActualWidth), Math.Min(160, Math.Max(1, section.ActualHeight))));
        }));
    }

    private static Control? FirstAction(DependencyObject root)
    {
        if (root is Control { IsVisible: true, IsEnabled: true, Focusable: true } control &&
            control is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.TextBoxBase or System.Windows.Controls.Primitives.Selector)
            return control;
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
            if (FirstAction(System.Windows.Media.VisualTreeHelper.GetChild(root, index)) is { } child) return child;
        return null;
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
