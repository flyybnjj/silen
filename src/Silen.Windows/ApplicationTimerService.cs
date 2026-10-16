using System.ComponentModel;
using System.Globalization;
using Silen.Core;

namespace Silen.Windows;

public sealed class ApplicationTimerService : IApplicationTimerService
{
    private readonly IApplicationTimerPlatform platform;
    private readonly IApplicationProcessProbe process;
    private readonly IApplicationTimerStore store;
    private readonly bool autoPoll;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource stop = new();
    private Task loop = Task.CompletedTask;
    private IDisposable? owner;
    private string[] profiles = [];
    private string status = "Sin perfiles de temporizador habilitados.";
    private bool ready, held;
    private uint requested;
    private int disposed;
    public ApplicationTimerService() : this(new NativeApplicationTimerPlatform(), new ApplicationProcessProbe(),
        new FileApplicationTimerStore(Path.Combine(AppDataPaths.Root, "application-timers")), true) { }
    internal ApplicationTimerService(IApplicationTimerPlatform platform, IApplicationProcessProbe process, IApplicationTimerStore store, bool autoPoll = false)
    { this.platform = platform; this.process = process; this.store = store; this.autoPoll = autoPoll; }
    public string Status => Volatile.Read(ref status);
    public bool Ready => Volatile.Read(ref ready) && Volatile.Read(ref disposed) == 0;
    public IReadOnlyList<string> Profiles => Array.AsReadOnly(Volatile.Read(ref profiles));
    public bool IsEnabled(string path) => Volatile.Read(ref profiles).Contains(path, StringComparer.OrdinalIgnoreCase);
    public TimerResolutionState? Inspect()
    {
        try { var state = platform.Read(); return state.Valid ? state : null; }
        catch (Exception ex) when (Expected(ex)) { return null; }
    }
    public async Task StartAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (Ready || disposed != 0) return;
            owner = store.AcquireOwner();
            var saved = await store.ReadAsync(token).ConfigureAwait(false);
            if (saved.Length > 64 || saved.Any(path => !ApplicationGpuPreference.HasPathShape(path))) throw new InvalidDataException("Perfil de temporizador incompatible; se conservó el archivo.");
            Volatile.Write(ref profiles, saved.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            Volatile.Write(ref ready, true);
            await Task.Run(TickLocked, token).ConfigureAwait(false);
            if (autoPoll) loop = Task.Run(PollAsync);
        }
        catch (OperationCanceledException)
        {
            try { ReleaseOwned(); } catch (Exception ex) when (Expected(ex)) { }
            finally { owner?.Dispose(); owner = null; Volatile.Write(ref ready, false); }
            SetStatus("Inicio del temporizador cancelado. Puedes volver a iniciarlo.");
            throw;
        }
        catch (Exception ex) when (Expected(ex))
        { owner?.Dispose(); owner = null; Volatile.Write(ref ready, false); SetStatus("Temporizador no iniciado: " + ex.Message); }
        finally { gate.Release(); }
    }
    public async Task<OptimizationOutcome> SetEnabledAsync(string path, bool enabled, CancellationToken token = default)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!Ready) return new(false, "El controlador de temporizador no está disponible. " + Status);
            if (enabled) path = ApplicationGpuPreference.ValidateExecutable(path);
            else if (!ApplicationGpuPreference.HasPathShape(path)) throw new ArgumentException("La ruta del perfil no es válida.");
            if (enabled && Inspect() is null) return new(false, "Windows no permite consultar el intervalo mínimo; no se guardó el perfil.");
            var previous = Volatile.Read(ref profiles);
            var next = previous.Where(item => !item.Equals(path, StringComparison.OrdinalIgnoreCase)).ToList();
            if (enabled) next.Add(path);
            if (next.Count > 64) return new(false, "Hay 64 perfiles guardados; desactiva uno antes de añadir otro.");
            if (previous.Contains(path, StringComparer.OrdinalIgnoreCase) == enabled) return new(true, "El perfil ya tiene ese estado.");
            await store.WriteAsync(next.ToArray(), token).ConfigureAwait(false);
            Volatile.Write(ref profiles, next.ToArray());
            await Task.Run(TickLocked).ConfigureAwait(false);
            return new(true, (enabled ? "Perfil guardado. Se activa con el producto mientras Silen esté abierto. " : "Perfil desactivado. ") + Status, true);
        }
        catch (Exception ex) when (Expected(ex)) { return new(false, ex.Message); }
        finally { gate.Release(); }
    }
    internal async Task TickAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try { if (Ready) await Task.Run(TickLocked, token).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    private void TickLocked()
    {
        try
        {
            if (disposed != 0) { ReleaseOwned(); return; }
            var saved = Volatile.Read(ref profiles);
            var states = saved.Select(path => process.IsRunning(path)).ToArray();
            if (!states.Any(state => state == true))
            {
                ReleaseOwned();
                SetStatus(saved.Length == 0 ? "Sin perfiles de temporizador habilitados." : states.Any(state => state is null)
                    ? "No se pudo comprobar un proceso; no se mantiene una solicitud sin identificarlo."
                    : "Esperando al juego o aplicación elegida. Silen debe permanecer abierto.");
                return;
            }
            var state = Inspect();
            if (state is null) { ReleaseOwned(); SetStatus("Windows no permite verificar el temporizador. Se volverá a comprobar."); return; }
            if (!held || requested != state.Finest || state.Current > state.Finest)
            {
                platform.Request(state.Finest); held = true; requested = state.Finest;
            }
            var observed = Inspect();
            SetStatus($"Solicitud propia: {Ms(requested)} ms · Windows informa {(observed is null ? "sin lectura" : Ms(observed.Current) + " ms")}. Experimental: Windows decide su efecto en cada proceso.");
        }
        catch (Exception ex) when (Expected(ex)) { SetStatus("No se confirmó el temporizador: " + ex.Message + " Se volverá a comprobar."); }
    }
    private void ReleaseOwned() { if (!held) return; platform.Release(); held = false; requested = 0; }
    private async Task PollAsync()
    {
        try { using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2)); while (await timer.WaitForNextTickAsync(stop.Token).ConfigureAwait(false)) await TickAsync(stop.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stop.Cancel(); await loop.ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            try { ReleaseOwned(); SetStatus("Silen liberó su solicitud de temporizador."); }
            catch (Exception ex) when (Expected(ex)) { SetStatus("Windows no confirmó la liberación: " + ex.Message); }
            finally { owner?.Dispose(); owner = null; Volatile.Write(ref ready, false); }
        }
        finally { gate.Release(); }
    }
    private void SetStatus(string value) => Volatile.Write(ref status, value);
    private static string Ms(uint value) => (value / 10000d).ToString("0.###", CultureInfo.GetCultureInfo("es-CL"));
    private static bool Expected(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException or Win32Exception or EntryPointNotFoundException or System.Text.Json.JsonException or DllNotFoundException;
}
