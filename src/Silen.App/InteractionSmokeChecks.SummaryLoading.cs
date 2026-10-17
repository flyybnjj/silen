using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Silen.Core;

namespace Silen.App;

internal static partial class InteractionSmokeChecks
{
    private static async Task RunSummaryAccessibilityChecksAsync(string directory, List<string> checks)
    {
        using var model = new MainViewModel(Path.Combine(directory, "summary-accessibility-history"), new ViewModelServices { CreateNativeTools = () => new FixtureOptimizationService() });
        model.ApplySnapshot(new SystemSnapshot { Samples = [new() { MemoryPercent = 50 }] }, isCurrent: true);
        var window = new MainWindow(model) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
            var root = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(window.Pages)!;
            static IEnumerable<System.Windows.Automation.Peers.AutomationPeer> Descendants(System.Windows.Automation.Peers.AutomationPeer peer)
            {
                foreach (var child in peer.GetChildren() ?? [])
                {
                    yield return child;
                    foreach (var descendant in Descendants(child)) yield return descendant;
                }
            }
            if (!Descendants(root).Any(peer => peer.GetName() == "Preparar optimización del PC sin aplicar cambios"))
                throw new InvalidOperationException("El contenido de Resumen y su acción principal deben aparecer en el árbol UI Automation aunque las cabeceras de pestaña estén ocultas.");
            if (window.Pages.Focusable || window.Pages.IsTabStop || ((ContentControl)window.FindName("SummaryOptimizationContent")).IsTabStop)
                throw new InvalidOperationException("Los contenedores sin acción no deben añadir paradas de Tab antes del botón principal.");
            model.SelectedPage = 1; await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ContextIdle);
            if (root.GetName() != "Contenido de RAM y red" || !Descendants(root).Any(peer => peer.GetName() == "Medir DNS"))
                throw new InvalidOperationException("El árbol accesible debe actualizar nombre y controles al cambiar de vista.");
            checks.Add("El contenedor de vistas expone la acción de Resumen en UI Automation sin pestañas visuales duplicadas");
        }
        finally { await model.PrepareToCloseAsync(); window.Close(); }
    }

    private static async Task RunSummaryLoadingPresentationChecksAsync(string directory, Dispatcher dispatcher, List<string> checks)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProgress<ScanProgress>? progress = null;
        var snapshot = new SystemSnapshot
        {
            Machine = new() { Manufacturer = "Equipo de prueba", Model = "Modelo", CpuName = "Procesador de prueba", GpuName = "Gráficos de prueba", TotalMemoryGb = 16 },
            Drives = [new() { Name = "C:", FreeGb = 140, TotalGb = 475 }],
            Samples = Enumerable.Range(0, 10).Select(index => new MetricSample { CpuPercent = 19 + index, MemoryPercent = 94, GpuPercent = 4, GpuTemperature = 60 }).ToList()
        };
        using var model = new MainViewModel(Path.Combine(directory, "summary-loading-history"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            Collect = async (_, token, detailed) =>
            {
                progress = detailed;
                detailed?.Report(new("Equipo", "Identidad leída.", 1, 14, Machine: snapshot.Machine));
                detailed?.Report(new("Unidades", "Unidades leídas.", 1, 14, Drives: snapshot.Drives));
                started.TrySetResult(); await release.Task.WaitAsync(token); return snapshot;
            },
            FindOem = (_, _, _) => Task.FromResult(new OemSupportResult(false, "Prueba", "", "", [], "Sin consulta externa", false))
        });
        var scan = model.ScanAsync(); await started.Task;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (model.SampleDetail != "Midiendo… 0/10 muestras")
            throw new InvalidOperationException("El contador de carga debe ser breve y no declarar una última lectura inexistente.");
        var window = new MainWindow(model) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        var observations = new List<object>();
        var locations = new Dictionary<string, (Rect Metric, Rect Action)>();
        T Named<T>(string name) where T : FrameworkElement => window.FindName(name) as T ?? throw new InvalidOperationException("Falta el elemento de carga " + name);
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Carga de Resumen: " + message); }
        Color Role(string name) => ((SolidColorBrush)Application.Current.Resources[name]).Color;
        Rect Bounds(FrameworkElement element) => element.TransformToAncestor(window).TransformBounds(new Rect(element.RenderSize));
        async Task Capture(string state)
        {
            foreach (var theme in new[] { "Claro", "Oscuro" })
            foreach (var size in new[] { new Size(1240, 820), new Size(1000, 680) })
            {
                ThemeManager.Apply(theme); window.Width = size.Width; window.Height = size.Height;
                var file = Path.Combine(directory, $"resumen-carga-{state}-{theme}-{size.Width}x{size.Height}.png");
                await RenderFixtureWindowAsync(window, file);
                var metrics = new[] { Named<TextBlock>("SummaryCpuMetric"), Named<TextBlock>("SummaryMemoryMetric"), Named<TextBlock>("SummaryGpuMetric") };
                var action = Visuals<Button>(window).Single(button => button.IsVisible && button.Command == model.PcOptimization.PrepareCommand);
                var viewport = Named<ScrollViewer>("SummaryScroll");
                var bars = Visuals<ProgressBar>(window).Count(bar => bar.IsVisible);
                var trace = Named<ResourceTrace>("SummaryCpuTrace");
                var highMemory = Named<TextBlock>("SummaryMemoryNotice");
                Require(metrics.All(metric => ((SolidColorBrush)metric.Foreground).Color == Role(state == "completo" ? "Text" : "Muted")), "las cifras parciales usan muted y solo el resultado terminado usa ink");
                if (state != "completo")
                {
                    Require(bars == 1, "solo debe verse una barra de diagnóstico");
                    Require(!Named<Grid>("FooterOperationStatus").IsVisible && !Named<ProgressBar>("FooterOperationProgress").IsVisible, "el pie no repite el estado ni el progreso del análisis");
                    Require(!action.IsEnabled && Visuals<TextBlock>(window).Any(text => text.IsVisible && text.Text == "Disponible al terminar el análisis"), "la acción bloqueada debe tener explicación visible");
                    var explanation = Visuals<TextBlock>(window).Single(text => text.IsVisible && text.Text == "Disponible al terminar el análisis");
                    Require(Bounds(explanation).Bottom <= Bounds(viewport).Bottom && Bounds(action).Bottom <= Bounds(viewport).Bottom, "la acción y su explicación deben caber completas sin desplazar la carga, también a 1000×680");
                    Require(!highMemory.IsVisible, "un pico provisional de memoria no muestra la recomendación final");
                    var expected = state == "0" ? "Midiendo… 0/10 muestras" : "Midiendo… 5/10 muestras";
                    Require(model.SampleDetail == expected, "el contador no incluye última lectura ni análisis pendiente");
                    Require(Visuals<TextBlock>(window).Any(text => text.IsVisible && text.Text == "Analizando tu equipo"), "el panel debe nombrar la actividad");
                    Require(!VisibleText(window, true).Any(text => text.Contains("CPU Midiendo") || text.Contains("Observando recursos: muestra")), "solo el panel informa del paso, sin otro contador");
                    var progressBar = Visuals<ProgressBar>(window).Single(bar => bar.IsVisible);
                    Require(((SolidColorBrush)progressBar.Foreground).Color == Role("Text") && ((SolidColorBrush)progressBar.Background).Color == Role("Line"), "el progreso usa ink y pista line, sin brick");
                    if (state == "0")
                    {
                        Require(metrics.All(metric => metric.Text == "—" && metric.FontSize <= 16 && metric.FontWeight == FontWeights.Normal), "los datos pendientes son marcadores discretos");
                        Require(trace.Visibility == Visibility.Hidden, "se reserva el espacio de traza sin dibujar líneas vacías");
                        locations[$"{theme}-{size.Width}"] = (Bounds(metrics[0]), Bounds(action));
                    }
                    else
                    {
                        Require(metrics[0].Text == "23%" && metrics.All(metric => metric.FontSize == 30), "las lecturas reales conservan su tamaño y son provisionales");
                        Require(trace.IsVisible, "la traza aparece a partir de cinco muestras");
                        var drawing = VisualTreeHelper.GetDrawing(trace);
                        Require(drawing is not null && drawing.Children.OfType<GeometryDrawing>().Count(item => item.Geometry is LineGeometry) == 4, "cinco muestras dibujan cuatro segmentos sin las tres líneas de rejilla");
                        var previous = locations[$"{theme}-{size.Width}"];
                        Require(Math.Abs(Bounds(metrics[0]).Y - previous.Metric.Y) < .5 && Math.Abs(Bounds(action).Y - previous.Action.Y) < .5, $"las métricas y la acción no se desplazan entre cero y cinco muestras: métrica {previous.Metric.Y} → {Bounds(metrics[0]).Y}, acción {previous.Action.Y} → {Bounds(action).Y}");
                    }
                    action.ApplyTemplate();
                    Require(action.Template.FindName("Body", action) is Border { Opacity: 1 }, "el botón desactivado no pierde contraste por opacidad");
                    if (size.Width == 1240) Require(viewport.ScrollableHeight < .5, "la carga debe caber a 1240×820 sin scrollbar");
                }
                else
                {
                    Require(bars == 0 && action.IsEnabled && highMemory.IsVisible && highMemory.Text.Contains("Memoria alta durante el análisis"), "al completar se habilita la acción y se contextualiza la memoria alta");
                }
                observations.Add(new { State = state, Theme = theme, size.Width, size.Height, VisibleProgressBars = bars,
                    viewport.ScrollableHeight, Scrollbar = viewport.ComputedVerticalScrollBarVisibility.ToString(),
                    Metric = Bounds(metrics[0]), Action = Bounds(action), Image = Path.GetFileName(file),
                    MetadataContrast = CaptureMetadataContrast(window, Named<TextBlock>("SummaryCpuTemperature"), Named<TextBlock>("SummaryMemoryCapacity"), Named<TextBlock>("SummaryGpuTemperature")) });
            }
        }
        try
        {
            window.Show(); await Capture("0");
            for (var index = 0; index < 5; index++) progress!.Report(new("Recursos", $"Muestra {index + 1} de 10 recibida.", index + 5, 14, snapshot.Samples[index]));
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Capture("5");
            release.TrySetResult(); Require(await scan, "el diagnóstico simulado termina"); await Capture("completo");
            model.ApplySnapshot(snapshot with { Samples = [new() { MemoryPercent = 90 }] }, isCurrent: true); await dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.Background);
            Require(!Named<TextBlock>("SummaryMemoryNotice").IsVisible, "90 % exacto no activa el aviso de más de 90 %");
            model.ApplySnapshot(snapshot with { Samples = [new() { MemoryPercent = null }] }, isCurrent: true); await dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.Background);
            Require(!Named<TextBlock>("SummaryMemoryNotice").IsVisible, "memoria desconocida no se convierte en una alerta");
            await File.WriteAllTextAsync(Path.Combine(directory, "resumen-carga-observaciones.json"), JsonSerializer.Serialize(new { FixtureOnly = true, Observations = observations }, ReportStore.JsonOptions));
            checks.Add("Carga de Resumen: 12 renders, un estado y progreso ink, marcador discreto y datos provisionales muted");
            checks.Add("La traza conserva el espacio y aparece desde cinco muestras sin rejilla ni desplazamiento de acciones");
            checks.Add("Memoria alta solo al terminar y sobre 90 %; desconocido y 90 % exacto no muestran el aviso");
            checks.Add("Optimizar PC bloqueado tiene explicación visible y texto sin opacidad; carga sin scrollbar a 1240×820");
        }
        finally { release.TrySetResult(); await model.PrepareToCloseAsync(); window.Close(); ThemeManager.Apply("Claro"); }
    }

    private static async Task RunSummaryMotionContrastChecksAsync(string directory, List<string> checks)
    {
        using var model = new MainViewModel(Path.Combine(directory, "motion-contrast-history"), new ViewModelServices { CreateNativeTools = () => new FixtureOptimizationService() });
        model.ApplySnapshot(new SystemSnapshot
        {
            Machine = new() { Manufacturer = "Equipo de prueba", Model = "Modelo", CpuName = "Procesador de prueba", GpuName = "Gráficos de prueba", TotalMemoryGb = 16 },
            Samples = Enumerable.Range(0, 10).Select(_ => new MetricSample { CpuPercent = 24, MemoryPercent = 94, GpuPercent = 4, GpuTemperature = 60 }).ToList()
        }, isCurrent: true);
        var window = new MainWindow(model) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        var observations = new List<object>();
        var minimum = double.PositiveInfinity;
        try
        {
            window.Show();
            foreach (var theme in new[] { "Claro", "Oscuro" })
            foreach (var size in new[] { new Size(1240, 820), new Size(1000, 680) })
            foreach (var second in new[] { 1.8, 2.8, 3.8, 4.8, 5.8, 6.8, 7.8, 8.8 })
            {
                ThemeManager.Apply(theme); window.Width = size.Width; window.Height = size.Height;
                window.CaptureMotionAt(second);
                var image = $"contraste-bloom-{theme}-{size.Width}x{size.Height}-{second.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}.png";
                await RenderFixtureWindowAsync(window, Path.Combine(directory, image));
                var contrast = CaptureMetadataContrast(window,
                    (TextBlock)window.FindName("SummaryCpuTemperature"), (TextBlock)window.FindName("SummaryMemoryCapacity"), (TextBlock)window.FindName("SummaryGpuTemperature"));
                var poseMinimum = JsonSerializer.SerializeToElement(contrast).EnumerateArray().Min(row => row.GetProperty("MinimumContrast").GetDouble());
                minimum = Math.Min(minimum, poseMinimum);
                observations.Add(new { Theme = theme, size.Width, size.Height, Second = second, Minimum = poseMinimum, Metadata = contrast, Image = image });
            }
            await File.WriteAllTextAsync(Path.Combine(directory, "resumen-contraste-poses.json"), JsonSerializer.Serialize(new { FixtureOnly = true, Minimum = minimum, Observations = observations,
                Limit = "32 poses deterministas de la animación existente, no una medida continua de todos los fotogramas, ni de fluidez/GPU o preferencias reales de Windows." }, ReportStore.JsonOptions));
            if (minimum < 4.5) throw new InvalidOperationException($"El contraste de metadatos sobre una pose de BLOOM es {minimum:F2}:1; requiere un fondo neutro local antes de acreditar AA.");
            checks.Add("Contraste de metadatos ≥4,5:1 en 32 poses temporizadas de BLOOM, claro/oscuro y ambos tamaños, sin modificar la ilustración");
        }
        finally { await model.PrepareToCloseAsync(); window.Close(); ThemeManager.Apply("Claro"); }
    }

    private static IReadOnlyList<object> CaptureMetadataContrast(Window window, params TextBlock[] texts)
    {
        var visual = (FrameworkElement)window.Content;
        foreach (var text in texts) text.Opacity = 0;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        foreach (var text in texts) text.Opacity = 1;
        var rows = new List<object>();
        static double Linear(byte component) { var value = component / 255d; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        static double Luminance(Color color) => .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        foreach (var text in texts)
        {
            var bounds = text.TransformToAncestor(visual).TransformBounds(new Rect(text.RenderSize));
            var region = new Int32Rect(Math.Max(0, (int)bounds.X), Math.Max(0, (int)bounds.Y), Math.Min((int)bounds.Width, bitmap.PixelWidth - Math.Max(0, (int)bounds.X)), Math.Min((int)bounds.Height, bitmap.PixelHeight - Math.Max(0, (int)bounds.Y)));
            if (region.Width <= 0 || region.Height <= 0) continue;
            var pixels = new byte[region.Width * region.Height * 4]; bitmap.CopyPixels(region, pixels, region.Width * 4, 0);
            var foreground = Luminance(((SolidColorBrush)text.Foreground).Color); var minimum = double.PositiveInfinity;
            for (var index = 0; index < pixels.Length; index += 4)
            {
                var background = Luminance(Color.FromRgb(pixels[index + 2], pixels[index + 1], pixels[index]));
                minimum = Math.Min(minimum, (Math.Max(foreground, background) + .05) / (Math.Min(foreground, background) + .05));
            }
            rows.Add(new { text.Name, MinimumContrast = minimum, Bounds = bounds, Method = "Peor fondo del rectángulo del texto con sus glifos ocultos; pose estática WPF, no todo el movimiento." });
        }
        return rows;
    }
}
