using System.IO;
using System.Text.Json;
using Silen.Core;

namespace Silen.App;

internal static partial class InteractionSmokeChecks
{
    private static async Task RunStartupDiagnosticsChecksAsync(string directory, List<string> checks)
    {
        for (var repetition = 0; repetition < 2; repetition++)
            await VerifyStartupDiagnosticsAsync(directory);
        checks.Add("Sonda optativa del arranque real: dos ejecuciones aisladas, fases completas y salida limitada a tiempos/contadores");
    }

    private static async Task VerifyStartupDiagnosticsAsync(string directory)
    {
        var output = Path.Combine(directory, "startup-diagnostics-fixture-" + Guid.NewGuid().ToString("N"));
        using var trace = StartupDiagnostics.Begin(output);
        using var model = new MainViewModel(Path.Combine(directory, "startup-diagnostics-history"), new ViewModelServices
        {
            CreateNativeTools = () => new FixtureOptimizationService(),
            Collect = (_, _, _) => Task.FromResult(new SystemSnapshot()),
            FindOem = (_, _, _) => Task.FromResult(new OemSupportResult(false, "Prueba", "", "", [], "Sin red en prueba", false))
        });
        await model.InitializeAsync(autoScan: true);
        await trace.SaveAsync(model);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "startup.json")));
        var phases = json.RootElement.GetProperty("Phases").EnumerateArray().Select(phase => phase.GetProperty("Name").GetString()).ToArray();
        if (!new[] { "InitializeAsync", "PreferencesAndHistory", "Collect", "OEM", "Tools", "WindowsUpdate", "Profiles" }.All(phases.Contains))
            throw new InvalidOperationException("La medición del arranque no cubre todas las fases reales de inicialización.");
        if (json.RootElement.GetProperty("ProductionEntryPoint").GetBoolean() || json.RootElement.TryGetProperty("Snapshot", out _) || json.RootElement.TryGetProperty("ApiKey", out _))
            throw new InvalidOperationException("La medición de arranque solo debe guardar tiempos y contadores, sin snapshot ni claves.");
        trace.Dispose();
        if (StartupDiagnostics.Measure("DisabledCheck") is not null)
            throw new InvalidOperationException("Al cerrar el diagnóstico no puede quedar un observador activo.");
        await model.PrepareToCloseAsync();
    }

}
