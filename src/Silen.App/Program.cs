using Silen.Windows;

namespace Silen.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // El ayudante no construye Application, recursos WPF ni clientes antes de COM.
        if (args.FirstOrDefault() == SystemActionHost.Argument)
            return SystemActionHost.RunAsync(args).GetAwaiter().GetResult();
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
