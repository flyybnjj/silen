using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Silen.Core;
using Silen.Windows;

namespace Silen.App;

public sealed record SavedTimerProfile(string Path) { public string Name => System.IO.Path.GetFileName(Path); }
public sealed class ApplicationSettingsViewModel : ObservableObject
{
    private readonly IUserPreferencesStore store;
    private readonly IApplicationTimerService timers;
    private readonly Func<string, Func<CancellationToken, Task>, Task<bool>> run;
    private bool expert, manual;
    private string theme = "Claro";
    private readonly Action<string> applyTheme;
    private string status = "El modo experto muestra controles individuales. Activarlo no modifica Windows.";
    internal ApplicationSettingsViewModel(IUserPreferencesStore store, IApplicationTimerService timers, Func<string, Func<CancellationToken, Task>, Task<bool>> run, Func<bool> canRun, Action<string>? applyTheme = null)
    {
        this.store = store; this.timers = timers; this.run = run;
        this.applyTheme = applyTheme ?? (_ => { });
        ApplyThemeCommand = new AsyncCommand(_ => ApplyThemeAsync(), _ => canRun(), ex => { status = ex.Message; Changed(null); });
        ToggleExpertCommand = new AsyncCommand(_ => ToggleExpertAsync(), _ => canRun(), ex => { status = ex.Message; Changed(null); });
        OpenManualCommand = new RelayCommand(_ => { manual = !manual; Changed(null); }, _ => canRun() && expert);
        DisableTimerCommand = new AsyncCommand(parameter => DisableTimerAsync(parameter?.ToString()), parameter => canRun() && parameter is string path && timers.IsEnabled(path), ex => { status = ex.Message; Changed(null); });
    }
    public bool ExpertMode => expert;
    public IReadOnlyList<string> Themes { get; } = ["Claro", "Oscuro", "Sistema"];
    public string SelectedTheme { get => theme; set { if (Themes.Contains(value)) Set(ref theme, value); } }
    public ICommand ApplyThemeCommand { get; }
    public bool ShowManual => expert && manual;
    public string Status => status;
    public string TimerStatus => timers.Status;
    public bool HasTimers => TimerProfiles.Count > 0;
    public ObservableCollection<SavedTimerProfile> TimerProfiles { get; } = [];
    public ICommand ToggleExpertCommand { get; }
    public ICommand OpenManualCommand { get; }
    public ICommand DisableTimerCommand { get; }
    internal async Task LoadAsync(CancellationToken token)
    {
        try { var saved = await store.ReadAsync(token); expert = saved.ExpertMode; theme = Themes.Contains(saved.Theme) ? saved.Theme : "Claro"; applyTheme(theme); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException) { status = "No se leyeron las preferencias. Se conserva el modo normal: " + ex.Message; }
        RefreshTimerProfiles(); Changed(null);
    }
    internal Task<bool> ToggleExpertAsync() => run("Guardando modo de uso…", async token =>
    {
        await store.WriteAsync(new(!expert, theme), token); expert = !expert; manual = false;
        status = expert ? "Modo experto disponible. Abre Optimizador manual para elegir cada cambio." : "Modo normal. Los respaldos siguen disponibles para restaurar.";
        Changed(null); CommandManager.InvalidateRequerySuggested();
    });
    private Task<bool> ApplyThemeAsync() => run("Guardando apariencia…", async token =>
    {
        await store.WriteAsync(new(expert, theme), token); applyTheme(theme);
        status = "Apariencia guardada. El alto contraste de Windows tiene prioridad."; Changed(null);
    });
    private Task<bool> DisableTimerAsync(string? path) => run("Desactivando perfil de temporizador…", async token =>
    {
        if (path is null) return;
        var result = await timers.SetEnabledAsync(path, false, token); status = result.Message; RefreshTimerProfiles(); Changed(null);
    });
    internal void RefreshTimerProfiles()
    {
        var paths = timers.Profiles;
        if (!TimerProfiles.Select(item => item.Path).SequenceEqual(paths, StringComparer.OrdinalIgnoreCase)) { TimerProfiles.Clear(); foreach (var path in paths) TimerProfiles.Add(new(path)); }
        Changed(nameof(HasTimers)); Changed(nameof(TimerStatus));
    }
}
