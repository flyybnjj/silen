using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Silen.App;


public static class Motion
{
    public const int SettleMilliseconds = 240;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty TransitionKeyProperty = DependencyProperty.RegisterAttached(
        "TransitionKey", typeof(object), typeof(Motion), new PropertyMetadata(null, OnTransitionChanged));

    public static readonly DependencyProperty RevealProperty = DependencyProperty.RegisterAttached(
        "Reveal", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnRevealChanged));

    private static readonly DependencyProperty ScaleProperty = DependencyProperty.RegisterAttached(
        "Scale", typeof(ScaleTransform), typeof(Motion));

    private static readonly DependencyProperty ClocksProperty = DependencyProperty.RegisterAttached(
        "Clocks", typeof(Dictionary<DependencyProperty, AnimationClock>), typeof(Motion));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static object? GetTransitionKey(DependencyObject element) => element.GetValue(TransitionKeyProperty);
    public static void SetTransitionKey(DependencyObject element, object? value) => element.SetValue(TransitionKeyProperty, value);
    public static bool GetReveal(DependencyObject element) => (bool)element.GetValue(RevealProperty);
    public static void SetReveal(DependencyObject element, bool value) => element.SetValue(RevealProperty, value);

    private static bool CanAnimate => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement element) return;
        element.PreviewMouseLeftButtonDown -= OnPressed;
        element.PreviewMouseLeftButtonUp -= OnReleased;
        element.MouseLeave -= OnReleased;
        element.LostMouseCapture -= OnReleased;
        element.PreviewKeyDown -= OnKeyDown;
        element.PreviewKeyUp -= OnKeyUp;
        element.LostKeyboardFocus -= OnReleased;
        element.IsEnabledChanged -= OnControlEnabledChanged;
        element.Unloaded -= OnUnloaded;
        if (!(bool)args.NewValue) { ResetScale(element); return; }
        element.PreviewMouseLeftButtonDown += OnPressed;
        element.PreviewMouseLeftButtonUp += OnReleased;
        element.MouseLeave += OnReleased;
        element.LostMouseCapture += OnReleased;
        element.PreviewKeyDown += OnKeyDown;
        element.PreviewKeyUp += OnKeyUp;
        element.LostKeyboardFocus += OnReleased;
        element.IsEnabledChanged += OnControlEnabledChanged;
        element.Unloaded += OnUnloaded;
    }

    private static void OnPressed(object sender, MouseButtonEventArgs args)
    {
        if (sender is FrameworkElement { IsEnabled: true } element) ScaleTo(element, .975, 90);
    }

    private static void OnReleased(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement element) ScaleTo(element, 1, 140);
    }

    private static void OnKeyDown(object sender, KeyEventArgs args)
    {
        if (!args.IsRepeat && args.Key is Key.Space or Key.Enter && sender is FrameworkElement { IsEnabled: true } element)
            ScaleTo(element, .975, 90);
    }

    private static void OnKeyUp(object sender, KeyEventArgs args)
    {
        if (args.Key is Key.Space or Key.Enter && sender is FrameworkElement element) ScaleTo(element, 1, 140);
    }

    private static void OnControlEnabledChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is FrameworkElement element && !(bool)args.NewValue) ResetScale(element);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement element) ResetScale(element);
    }

    private static ScaleTransform GetScale(FrameworkElement element)
    {
        if (element.GetValue(ScaleProperty) is ScaleTransform scale) return scale;
        scale = new ScaleTransform(1, 1);
        var transforms = new TransformGroup();
        if (element.RenderTransform is { } existing && !existing.Value.IsIdentity) transforms.Children.Add(existing);
        transforms.Children.Add(scale);
        element.SetCurrentValue(UIElement.RenderTransformProperty, transforms);
        element.SetCurrentValue(UIElement.RenderTransformOriginProperty, new Point(.5, .5));
        element.SetValue(ScaleProperty, scale);
        return scale;
    }

    private static void ScaleTo(FrameworkElement element, double target, int milliseconds)
    {
        if (!CanAnimate || !element.IsLoaded) { ResetScale(element); return; }
        var scale = GetScale(element);
        Animate(scale, ScaleTransform.ScaleXProperty, scale.ScaleX, target, milliseconds);
        Animate(scale, ScaleTransform.ScaleYProperty, scale.ScaleY, target, milliseconds);
    }

    private static void ResetScale(FrameworkElement element)
    {
        if (element.GetValue(ScaleProperty) is not ScaleTransform scale) return;
        RemoveClock(scale, ScaleTransform.ScaleXProperty);
        RemoveClock(scale, ScaleTransform.ScaleYProperty);
        scale.ScaleX = scale.ScaleY = 1;
    }

    private static void OnTransitionChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement element || !element.IsLoaded || Equals(args.OldValue, args.NewValue)) return;
        var from = Math.Min(element.Opacity, .68);
        RemoveClock(element, UIElement.OpacityProperty);
        if (!CanAnimate) { element.SetCurrentValue(UIElement.OpacityProperty, 1d); return; }
        Animate(element, UIElement.OpacityProperty, from, 1, 160);
    }

    private static void OnRevealChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement element) return;
        var opacity = (bool)args.NewValue ? 1d : 0d;
        var from = element.Opacity;
        RemoveClock(element, UIElement.OpacityProperty);
        if (!CanAnimate || !element.IsLoaded) { element.SetCurrentValue(UIElement.OpacityProperty, opacity); return; }
        Animate(element, UIElement.OpacityProperty, from, opacity, 120);
    }

    private static void RemoveClock(DependencyObject target, DependencyProperty property)
    {
        if (target.GetValue(ClocksProperty) is Dictionary<DependencyProperty, AnimationClock> clocks)
            clocks.Remove(property);
        ApplyClock(target, property, null);
    }

    private static void ApplyClock(DependencyObject target, DependencyProperty property, AnimationClock? clock)
    {
        if (target is Animatable animationTarget) animationTarget.ApplyAnimationClock(property, clock, HandoffBehavior.SnapshotAndReplace);
        else if (target is UIElement element) element.ApplyAnimationClock(property, clock, HandoffBehavior.SnapshotAndReplace);
    }

    private static void Animate(DependencyObject target, DependencyProperty property, double from, double to, int milliseconds)
    {
        RemoveClock(target, property);
        if (target.GetValue(ClocksProperty) is not Dictionary<DependencyProperty, AnimationClock> clocks)
        {
            clocks = new Dictionary<DependencyProperty, AnimationClock>();
            target.SetValue(ClocksProperty, clocks);
        }
        var clock = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        }.CreateClock();
        clocks[property] = clock;
        clock.Completed += (_, _) =>
        {

            if (!clocks.TryGetValue(property, out var current) || !ReferenceEquals(current, clock)) return;
            RemoveClock(target, property);
            target.SetCurrentValue(property, to);
        };
        ApplyClock(target, property, clock);
    }
}
