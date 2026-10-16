namespace Silen.Windows;

internal static class ApplicationGpuPreference
{
    internal static bool HasPathShape(string path) => !string.IsNullOrWhiteSpace(path) && path.Length < 32767 &&
        !path.Any(char.IsControl) && !path.StartsWith(@"\\", StringComparison.Ordinal) && Path.IsPathFullyQualified(path) &&
        Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) && Path.GetFullPath(path).Equals(path, StringComparison.OrdinalIgnoreCase);
    internal static string ValidateExecutable(string path)
    {
        if (!HasPathShape(path)) throw new ArgumentException("Elige el archivo .exe local de un juego o aplicación.");
        if (!File.Exists(path)) throw new InvalidOperationException("El ejecutable ya no existe. Vuelve a elegirlo.");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Elige el ejecutable real, no un enlace.");
        for (var parent = Directory.GetParent(path); parent is not null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("La ruta contiene una redirección. Elige la ubicación real del juego.");
        return path;
    }
}

public sealed partial class NativeOptimizationService
{
    public bool? ReadApplicationGpuPreference(string executablePath)
    {
        try
        {
            var path = ApplicationGpuPreference.ValidateExecutable(executablePath);
            return FortniteGpuPreference.IsHighPerformance(platform.ReadValue(OptimizationTargets.GpuPreferences, path));
        }
        catch (Exception ex) when (IsExpectedFailure(ex)) { return null; }
    }

    public Task<OptimizationOutcome> SetApplicationGpuPreferenceAsync(string executablePath, CancellationToken cancellationToken = default) =>
        ExecuteAsync(_ =>
        {
            var path = ApplicationGpuPreference.ValidateExecutable(executablePath);
            var previous = platform.ReadValue(OptimizationTargets.GpuPreferences, path);
            var intended = RegistryValueState.String(FortniteGpuPreference.HighPerformanceText(previous));
            if (!previous.Same(intended) && !platform.CanWriteKey(OptimizationTargets.GpuPreferences)) throw new UnauthorizedAccessException("Windows no permite guardar la preferencia GPU.");
            return NewEntry("application-gpu", "GPU de alto rendimiento · " + Path.GetFileName(path), [new("Registry", OptimizationTargets.GpuPreferences, path, previous, intended)]);
        }, cancellationToken);
}
