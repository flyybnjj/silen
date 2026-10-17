using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Silen.Core;

namespace Silen.App;

internal sealed class RibbonAnimator : IDisposable
{
    private readonly Window window;
    private readonly MainViewModel model;
    private readonly SilenRibbon artwork;
    private readonly FrameworkElement content;
    private readonly FrameworkElement veil;
    private readonly Stopwatch clock = new();
    private readonly BlurEffect blur = new() { KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
    private readonly ScaleTransform scale = new();
    private readonly TranslateTransform translation = new();
    private bool captureMode;
    private bool loaded;
    private bool disposed;
    private bool attached;
    private double offset;
    private double lastFrame = double.NegativeInfinity;

    internal RibbonAnimator(Window window, MainViewModel model, SilenRibbon artwork, FrameworkElement content, FrameworkElement veil, bool animateStartup)
    {
        this.window = window;
        this.model = model;
        this.artwork = artwork;
        this.content = content;
        this.veil = veil;
        captureMode = !animateStartup;
        offset = captureMode ? RibbonMotionTimeline.IntroDuration : 0;
        artwork.Effect = blur;
        artwork.RenderTransformOrigin = new Point(.5, .5);
        var transform = new TransformGroup();
        transform.Children.Add(scale);
        transform.Children.Add(translation);
        artwork.RenderTransform = transform;
        RenderOptions.SetBitmapScalingMode(artwork, BitmapScalingMode.HighQuality);
        window.Loaded += OnLoaded;
        window.Activated += OnPolicyChanged;
        window.Deactivated += OnDeactivated;
        window.StateChanged += OnPolicyChanged;
        window.IsVisibleChanged += OnVisibilityChanged;
        window.SizeChanged += OnSizeChanged;
        window.PreviewMouseDown += OnPointerInput;
        window.PreviewKeyDown += OnKeyInput;
        window.Unloaded += OnUnloaded;
        model.PropertyChanged += OnModelChanged;
        model.Bloom.PropertyChanged += OnBloomChanged;
        SystemParameters.StaticPropertyChanged += OnPreferencesChanged;
        ApplyFrame(offset);
    }

    internal bool IsRunning => attached;
    internal long RenderedFrames { get; private set; }
    internal double ElapsedSeconds => offset + clock.Elapsed.TotalSeconds;
    internal double LastRenderMilliseconds { get; private set; }
    internal double MaximumRenderMilliseconds { get; private set; }
    private bool ReducedMotion => !SystemParameters.ClientAreaAnimation || (RenderCapability.Tier >> 16) == 0;

    internal void CaptureAt(double seconds)
    {
        if (disposed) return;
        captureMode = true;
        Stop();
        clock.Reset();
        offset = double.IsFinite(seconds) ? Math.Max(0, seconds) : 0;
        artwork.Visibility = Visibility.Visible;
        ApplyFrame(offset);
    }

    internal void CaptureArtworkAt(double phase)
    {
        CaptureAt(0);
        artwork.Opacity = 1;
        artwork.RenderFrame(phase);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        loaded = true;
        RefreshPolicy();
        // Background launches still present a readable app without waiting for focus.
        _ = window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (disposed || !loaded || captureMode) return;
            if (!window.IsActive) FinishIntro();
            RefreshPolicy();
        }));
    }
    private void OnUnloaded(object sender, RoutedEventArgs e) { loaded = false; Stop(); }
    private void OnPolicyChanged(object? sender, EventArgs e) => RefreshPolicy();
    private void OnDeactivated(object? sender, EventArgs e) { FinishIntro(); RefreshPolicy(); }
    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue) FinishIntro();
        RefreshPolicy();
    }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ApplyFrame(ElapsedSeconds);
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(MainViewModel.IsBusy) or nameof(MainViewModel.IsScanning)) RefreshPolicy();
    }
    private void OnBloomChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(BloomViewModel.State))
        { if (!captureMode && !disposed) { ApplyFrame(ElapsedSeconds); RefreshPolicy(); } }
    }
    private void OnPreferencesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (disposed) return;
        if (window.Dispatcher.CheckAccess()) RefreshPolicy();
        else _ = window.Dispatcher.BeginInvoke(new Action(() => { if (!disposed) RefreshPolicy(); }));
    }
    private void OnPointerInput(object sender, MouseButtonEventArgs e) => FinishIntro();
    private void OnKeyInput(object sender, KeyEventArgs e) => FinishIntro();

    private void FinishIntro()
    {
        if (disposed || captureMode || ElapsedSeconds >= RibbonMotionTimeline.IntroDuration) return;
        offset += RibbonMotionTimeline.IntroDuration - ElapsedSeconds;
        ApplyFrame(ElapsedSeconds);
        RefreshPolicy();
    }

    private void RefreshPolicy()
    {
        if (disposed) return;
        if (captureMode) { Stop(); return; }
        if (SystemParameters.HighContrast || ReducedMotion)
        {
            Stop();
            offset += Math.Max(0, RibbonMotionTimeline.IntroDuration - ElapsedSeconds);
            ApplyFrame(ElapsedSeconds);
            artwork.Visibility = SystemParameters.HighContrast ? Visibility.Hidden : Visibility.Visible;
            if (SystemParameters.HighContrast) veil.Opacity = 0;
            return;
        }
        artwork.Visibility = Visibility.Visible;
        var frame = RibbonMotionTimeline.Evaluate(ElapsedSeconds);
        var canRun = RibbonMotionTimeline.CanRun(loaded, window.IsVisible, window.IsActive,
            window.WindowState == WindowState.Minimized, false, false, false, model.IsBusy, frame.IntroComplete);
        if (canRun && !attached)
        {
            lastFrame = double.NegativeInfinity;
            clock.Start();
            CompositionTarget.Rendering += OnRendering;
            attached = true;
        }
        else if (!canRun) { Stop(); ApplyFrame(ElapsedSeconds); }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (disposed) return;
        var seconds = ElapsedSeconds;
        var complete = seconds >= RibbonMotionTimeline.IntroDuration;
        var interval = complete ? 1d / 24 : 1d / 30;
        if (seconds - lastFrame < interval) return;
        lastFrame = seconds;
        ApplyFrame(seconds);
        if (complete && model.IsBusy) RefreshPolicy();
    }

    private void ApplyFrame(double seconds)
    {
        if (disposed) return;
        var frame = RibbonMotionTimeline.Evaluate(seconds);
        var began = Stopwatch.GetTimestamp();
        var state = model.IsScanning ? BloomState.Analyzing : model.Bloom.State;
        var pose = state switch { BloomState.Analyzing => .14, BloomState.Thinking => .32, BloomState.Success => .0, BloomState.Warning => .56, BloomState.Error => .72, _ => .0 };
        var phase = !captureMode && model.IsBusy ? pose : frame.Phase / Math.Tau + (!captureMode ? pose : 0);
        artwork.RenderFrame(!captureMode && ReducedMotion ? 0 : phase);
        blur.Radius = frame.BlurRadius;
        artwork.Opacity = frame.ArtworkOpacity;
        // La legibilidad está disponible desde el primer fotograma. La mascota
        // conserva su transformación, opacidad, desenfoque y cadencia.
        veil.Opacity = .75;
        scale.ScaleX = scale.ScaleY = .62 + .40 * frame.BackdropBlend;
        translation.X = Math.Max(0, window.ActualWidth) * .18 * frame.BackdropBlend;
        translation.Y = Math.Max(0, window.ActualHeight) * .04 * frame.BackdropBlend;
        content.Opacity = 1;
        content.IsHitTestVisible = true;
        RenderedFrames++;
        LastRenderMilliseconds = Stopwatch.GetElapsedTime(began).TotalMilliseconds;
        MaximumRenderMilliseconds = Math.Max(MaximumRenderMilliseconds, LastRenderMilliseconds);
    }

    private void Stop()
    {
        clock.Stop();
        if (!attached) return;
        CompositionTarget.Rendering -= OnRendering;
        attached = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        Stop();
        disposed = true;
        window.Loaded -= OnLoaded;
        window.Activated -= OnPolicyChanged;
        window.Deactivated -= OnDeactivated;
        window.StateChanged -= OnPolicyChanged;
        window.IsVisibleChanged -= OnVisibilityChanged;
        window.SizeChanged -= OnSizeChanged;
        window.PreviewMouseDown -= OnPointerInput;
        window.PreviewKeyDown -= OnKeyInput;
        window.Unloaded -= OnUnloaded;
        model.PropertyChanged -= OnModelChanged;
        model.Bloom.PropertyChanged -= OnBloomChanged;
        SystemParameters.StaticPropertyChanged -= OnPreferencesChanged;
        content.Opacity = 1;
        content.IsHitTestVisible = true;
    }
}
