using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Silen.Core;

namespace Silen.App;

public sealed record FindingRow(Finding Finding)
{
    public string Title => Finding.Title;
    public string Description => Finding.Description;
    public string Evidence => Finding.Evidence;
    public string Category => Finding.Category;
    public bool HasAction => Finding.Action != ActionKind.None;
    public string Level => Finding.Severity switch { Severity.Critical => "Atención prioritaria", Severity.Warning => "Revisar", Severity.Suggestion => "Sugerencia", _ => "Información" };
    public string Accent => Finding.Severity is Severity.Critical or Severity.Warning ? "#B23B35" : "#292B27";
    public string ActionLabel => Labels.Action(Finding.Action);
}

public sealed record HardwareRow(string Name, string Value);
public sealed record GameMetric(string Name, string Baseline, string Comparison, string Difference);
public sealed record ProcessChoice(string Name, int Id, double MemoryMb)
{
    public string Label => $"{Name} · PID {Id} · {Labels.Number(MemoryMb, " MB", 0)}";
}
public sealed record SessionDisplay(GamingSession Session)
{
    public string Name => string.IsNullOrWhiteSpace(Session.ProcessName) ? System.IO.Path.GetFileName(Session.SourceFile) : Session.ProcessName;
    public string Date => Session.CapturedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm", CultureInfo.GetCultureInfo("es-CL"));
    public string Fps => Session.AverageFps.ToString("N1", Labels.Culture);
    public string Low => Session.OnePercentLowFps.ToString("N1", Labels.Culture);
    public string Frames => $"{SpanishText.Count(Session.FrameCount, "fotograma", "fotogramas")} · {SpanishText.Count(Session.IgnoredRows, "fila descartada", "filas descartadas")}";
    public string Source => Session.SourceFile;
}

public static class Labels
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-CL");
    public static string Known(string? value) => string.IsNullOrWhiteSpace(value) ? "No disponible" : value;
    public static string Number(double? value, string unit, int decimals = 0) => value is { } v && double.IsFinite(v) ? $"{v.ToString($"N{decimals}", Culture)}{unit}" : "No disponible";
    public static string Action(ActionKind action) => action switch
    {
        ActionKind.OpenStartup => "Revisar inicio", ActionKind.OpenStorage => "Abrir almacenamiento",
        ActionKind.OpenPower => "Revisar energía", ActionKind.OpenWindowsUpdate => "Abrir Windows Update",
        ActionKind.OpenOptionalDrivers => "Ver drivers opcionales", ActionKind.OpenGraphics => "Abrir gráficos",
        ActionKind.OpenGameMode => "Abrir Game Mode", ActionKind.OpenManufacturer => "Ver fabricante",
        ActionKind.OpenTaskManager => "Ver procesos", _ => "Ver guía"
    };
}

public sealed class BooleanVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var show = value is true;
        if (parameter?.ToString() == "inverse") show = !show;
        return show ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class SignatureConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool signed ? signed ? "Firmado" : "Sin firma" : "Sin dato";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ProblemConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is int code ? code == 0 ? "Sin incidencia" : $"Código {code}" : "Sin dato";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class KnownStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => string.IsNullOrWhiteSpace(value?.ToString()) ? parameter?.ToString() ?? "Sin dato" : value.ToString()!;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
