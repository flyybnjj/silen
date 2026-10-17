using System.Windows;
using System.Windows.Media;

namespace Silen.App;


public sealed class ResourceTrace : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(IReadOnlyList<double?>), typeof(ResourceTrace), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LimitProperty = DependencyProperty.Register(nameof(Limit), typeof(double), typeof(ResourceTrace), new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(ResourceTrace), new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(41, 43, 39)), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(nameof(ShowGrid), typeof(bool), typeof(ResourceTrace), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<double?>? Values { get => (IReadOnlyList<double?>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public double Limit { get => (double)GetValue(LimitProperty); set => SetValue(LimitProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public bool ShowGrid { get => (bool)GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var gridPen = new Pen(TryFindResource("Line") as Brush ?? new SolidColorBrush(Color.FromRgb(216, 214, 207)), 1);
        if (ShowGrid)
            for (var i = 0; i < 3; i++) dc.DrawLine(gridPen, new(0, ActualHeight * i / 2), new(ActualWidth, ActualHeight * i / 2));
        if (Values is not { Count: > 0 } values || ActualWidth <= 0 || ActualHeight <= 0 || Limit <= 0) return;
        Point? previous = null;
        var pen = new Pen(Stroke, 2);
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } value || !double.IsFinite(value)) { previous = null; continue; }
            var point = new Point(values.Count == 1 ? ActualWidth / 2 : ActualWidth * i / (values.Count - 1), ActualHeight * (1 - Math.Clamp(value / Limit, 0, 1)));
            if (previous is { } last) dc.DrawLine(pen, last, point);
            else dc.DrawEllipse(Stroke, null, point, 2, 2);
            previous = point;
        }
    }
}
