using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Silen.App;

internal static partial class InteractionSmokeChecks
{
    private static async Task RunRealUsePresentationChecksAsync(string directory, List<string> checks)
    {
        using var model = new MainViewModel(Path.Combine(directory, "real-use"), new ViewModelServices { CreateNativeTools = () => new FixtureOptimizationService() });
        var window = new MainWindow(model, animateStartup: true) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); window.UpdateLayout();
            if (window.FindName("StartupBloomPanel") is FrameworkElement panel && Visuals<TextBlock>(panel).Any(text => !string.IsNullOrWhiteSpace(text.Text)))
                throw new InvalidOperationException("La carga inicial debe ser solo Bloom, sin frases, tarjeta ni pantalla blanca.");
            checks.Add("Carga sin frases ni panel explicativo; la presencia visual pertenece solo a Bloom");
            using var splash = new BloomSplashWindow(() => { }) { ShowActivated = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
            splash.Show();
            if (!splash.AllowsTransparency || splash.WindowStyle != WindowStyle.None || splash.ShowInTaskbar || splash.Width > 300 ||
                splash.Background is not System.Windows.Media.SolidColorBrush { Color.A: 0 } || Visuals<TextBlock>(splash).Any())
                throw new InvalidOperationException("El inicio real debe ser Bloom en una ventana compacta, transparente y sin palabras.");
            splash.CaptureAt(0); await RenderFixtureWindowAsync(splash, Path.Combine(directory, "inicio-transparente-0.png"));
            splash.CaptureAt(.3); await RenderFixtureWindowAsync(splash, Path.Combine(directory, "inicio-transparente-1.png"));
            if (File.ReadAllBytes(Path.Combine(directory, "inicio-transparente-0.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(directory, "inicio-transparente-1.png"))))
                throw new InvalidOperationException("Bloom debe cambiar de pose al volar.");
            using (var source = File.OpenRead(Path.Combine(directory, "inicio-transparente-1.png")))
            {
                var decoder = new System.Windows.Media.Imaging.PngBitmapDecoder(source, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                var pixels = new System.Windows.Media.Imaging.FormatConvertedBitmap(decoder.Frames[0], System.Windows.Media.PixelFormats.Bgra32, null, 0);
                var first = new byte[4]; pixels.CopyPixels(new Int32Rect(0, 0, 1, 1), first, 4, 0);
                if (first[3] != 0) throw new InvalidOperationException("El fondo real de la carga debe conservar transparencia, no pintarse blanco ni negro.");
            }
            checks.Add("Ventana real de carga transparente, sin marco, texto, fondo ni entrada adicional en la barra de tareas");
            model.ApplySnapshot(new Silen.Core.SystemSnapshot { Machine = new() { Manufacturer = "Lenovo", Model = "Equipo de prueba", DeviceKind = Silen.Core.DeviceKind.Laptop, CpuName = "Procesador de prueba", GpuName = "Gráficos de prueba", TotalMemoryGb = 16 }, Samples = [new() { CpuPercent = 23, MemoryPercent = 60, GpuPercent = 12, GpuTemperature = 54 }] }, true);
            await model.Tools.RefreshAsync(); model.Activity.Dismiss();
            foreach (var theme in new[] { "Oscuro", "Claro" })
            foreach (var size in new[] { new Size(1240, 820), new Size(1000, 680) })
            {
                ThemeManager.Apply(theme); window.Width = size.Width; window.Height = size.Height;
                foreach (var page in new[] { 0, 1, 2, 3, 4, 5 })
                {
                    model.SelectedPage = page;
                    await RenderFixtureWindowAsync(window, Path.Combine(directory, $"rediseno-{page}-{theme}-{size.Width}.png"));
                }
            }
            model.SelectedPage = 0;
            await model.PcOptimization.PrepareAsync();
            model.PcOptimization.OptionCategory = model.PcOptimization.OptionCategories.First(category => category != "Todas");
            await model.PcOptimization.PrepareAsync();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            if (!model.PcOptimization.VisibleOptions.Any()) throw new InvalidOperationException("Actualizar disponibilidad no puede dejar el filtro de categoría sin resultados por un SelectedItem transitorio nulo.");
            var chosen = model.PcOptimization.Options.First(row => row.Option.Available);
            chosen.Selected = true; model.PcOptimization.OptionSearch = "sin coincidencia";
            if (model.PcOptimization.VisibleOptions.Any() || !chosen.Selected || !model.PcOptimization.ApplyCommand.CanExecute(null)) throw new InvalidOperationException("Filtrar no puede borrar selecciones ni cambiar qué se aplicará.");
            model.PcOptimization.OptionSearch = "";
            model.CloseOptimizationDialogCommand.Execute(null);
            checks.Add("Búsqueda de opciones conserva la selección completa, aunque un ajuste quede oculto por el filtro");
        }
        finally { await model.PrepareToCloseAsync(); window.Close(); ThemeManager.Apply("Claro"); }
        var scroll = new ScrollViewer { Content = new Border { Height = 1800 }, CanContentScroll = false };
        CalmScroll.SetEnabled(scroll, true);
        var scrollingWindow = new Window { Content = scroll, Width = 320, Height = 220, ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        try
        {
            scrollingWindow.Show(); scrollingWindow.UpdateLayout();
            var wheel = new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
            scroll.RaiseEvent(wheel);
            await Task.Delay(220); await scrollingWindow.Dispatcher.InvokeAsync(scrollingWindow.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
            if (SystemParameters.ClientAreaAnimation && (!wheel.Handled || scroll.VerticalOffset <= 0 || scroll.VerticalOffset > scroll.ScrollableHeight))
                throw new InvalidOperationException("La rueda debe desplazar píxeles dentro del rango, sin desplazar la selección.");
            checks.Add("Rueda por píxeles con límites y alternativa nativa cuando Windows reduce animaciones");
        }
        finally { scrollingWindow.Close(); }
        var detailsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancelledDetails = new MainViewModel(Path.Combine(directory, "cancelled-memory-enrichment"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(), Collect = (_, _, _) => Task.FromResult(new Silen.Core.SystemSnapshot()),
            ReadMemory = automatic => new(16L << 30, 8L << 30, 8L << 30, 24L << 30, automatic),
            ReadAutomaticPagefile = async token => { detailsStarted.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return true; },
            ReadMemoryProcesses = _ => Task.FromResult<IReadOnlyList<Silen.Core.ProcessInfo>>([])
        });
        var scan = cancelledDetails.ScanAsync(); await detailsStarted.Task;
        cancelledDetails.CancelCommand.Execute(null); await scan;
        if (!cancelledDetails.HasCompletedResult || !cancelledDetails.Status.StartsWith("Análisis completado.") || cancelledDetails.Status.Contains("descartados"))
            throw new InvalidOperationException("Cancelar el enriquecimiento independiente de RAM conserva y rotula el diagnóstico ya terminado; nunca afirma haberlo descartado.");
        checks.Add("Cancelar detalles adicionales de RAM conserva explícitamente el diagnóstico completo y omite más consultas");
    }
}
