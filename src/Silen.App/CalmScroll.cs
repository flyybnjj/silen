using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Silen.App;

/// <summary>Desplazamiento breve por píxeles; conserva tablas virtualizadas y reducir movimiento.</summary>
public static class CalmScroll
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(CalmScroll), new PropertyMetadata(false, Changed));
    private static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached("Offset", typeof(double), typeof(CalmScroll), new PropertyMetadata(0d, OffsetChanged));
    private static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached("Target", typeof(double), typeof(CalmScroll), new PropertyMetadata(double.NaN));
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not ScrollViewer viewer) return;
        if ((bool)args.OldValue) { viewer.PreviewMouseWheel -= Wheel; viewer.Unloaded -= Unloaded; viewer.PreviewMouseDown -= StopForPointer; viewer.PreviewKeyDown -= StopForKey; }
        if ((bool)args.NewValue) { viewer.PreviewMouseWheel += Wheel; viewer.Unloaded += Unloaded; viewer.PreviewMouseDown += StopForPointer; viewer.PreviewKeyDown += StopForKey; }
    }
    private static void OffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs args) => ((ScrollViewer)target).ScrollToVerticalOffset((double)args.NewValue);
    private static void Unloaded(object sender, RoutedEventArgs args)
    {
        var viewer = (ScrollViewer)sender;
        Stop(viewer);
    }
    private static void StopForPointer(object sender, MouseButtonEventArgs args) => Stop((ScrollViewer)sender);
    private static void StopForKey(object sender, KeyEventArgs args) => Stop((ScrollViewer)sender);
    private static void Stop(ScrollViewer viewer)
    {
        viewer.SetCurrentValue(OffsetProperty, viewer.VerticalOffset);
        viewer.BeginAnimation(OffsetProperty, null); viewer.SetValue(TargetProperty, double.NaN);
    }
    private static void Wheel(object sender, MouseWheelEventArgs args)
    {
        var viewer = (ScrollViewer)sender;
        if (args.Handled || viewer.CanContentScroll || viewer.ScrollableHeight <= 0 || !SystemParameters.ClientAreaAnimation) return;
        // El visor interno recibe primero su rueda; no arrebatar selección ni scroll de una tabla.
        for (var source = args.OriginalSource as DependencyObject; source is not null && source != viewer; source = source is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(source) : null)
            if (source is ScrollViewer) return;
        var previous = (double)viewer.GetValue(TargetProperty);
        var current = viewer.VerticalOffset;
        var target = Math.Clamp((double.IsNaN(previous) ? current : previous) - args.Delta / 120d * Math.Max(16, SystemParameters.WheelScrollLines * 16), 0, viewer.ScrollableHeight);
        if (Math.Abs(target - current) < .1) return;
        viewer.SetValue(TargetProperty, target);
        var animation = new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(140)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        animation.Completed += (_, _) => viewer.SetValue(TargetProperty, double.NaN);
        viewer.BeginAnimation(OffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        args.Handled = true;
    }
}
