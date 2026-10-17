using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Silen.App;

/// <summary>Presencia transitoria de carga: sin tarjeta, texto, cromo ni fondo.</summary>
internal sealed class BloomSplashWindow : Window, IDisposable
{
    private readonly SilenRibbon bloom = new();
    private readonly TranslateTransform flight = new();
    private readonly DispatcherTimer timer;
    private readonly Stopwatch clock = new();
    private readonly Action cancel;
    private bool finished;
    internal BloomSplashWindow(Action cancel)
    {
        this.cancel = cancel;
        Width = 280; Height = 240; WindowStyle = WindowStyle.None;
        AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        System.Windows.Automation.AutomationProperties.SetName(this, "Leyendo los datos del equipo");
        bloom.Width = 260; bloom.Height = 210; bloom.RenderTransform = flight;
        Content = bloom;
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1d / 24) };
        timer.Tick += Render;
        Loaded += Policy; Activated += Policy; Deactivated += Policy;
        IsVisibleChanged += VisibilityChanged;
        SystemParameters.StaticPropertyChanged += PreferencesChanged;
        PreviewKeyDown += (_, args) => { if (args.Key == Key.Escape) { cancel(); args.Handled = true; } };
        bloom.RenderFrame(0);
    }
    private void Policy(object? sender, EventArgs args)
    {
        if (!finished && IsLoaded && IsVisible && IsActive && SystemParameters.ClientAreaAnimation)
        { clock.Start(); timer.Start(); }
        else { timer.Stop(); clock.Stop(); }
    }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) => Policy(sender, EventArgs.Empty);
    private void PreferencesChanged(object? sender, PropertyChangedEventArgs args) => Dispatcher.InvokeAsync(() => Policy(sender, EventArgs.Empty));
    private void Render(object? sender, EventArgs args)
    {
        var phase = clock.Elapsed.TotalSeconds / 3.6;
        bloom.RenderFrame(phase); flight.Y = Math.Sin(phase * Math.Tau) * 8;
    }
    internal void CaptureAt(double phase) { timer.Stop(); bloom.RenderFrame(phase); flight.Y = Math.Sin(phase * Math.Tau) * 8; }
    protected override void OnClosing(CancelEventArgs args)
    {
        if (!finished) { args.Cancel = true; cancel(); }
        base.OnClosing(args);
    }
    public void Dispose()
    {
        if (finished) return;
        finished = true; timer.Stop(); clock.Stop(); timer.Tick -= Render;
        SystemParameters.StaticPropertyChanged -= PreferencesChanged;
        Close(); Content = null;
    }
}
