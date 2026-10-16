using System.Windows.Input;
using Silen.Windows;

namespace Silen.App;

public sealed class AppUpdateViewModel : ObservableObject
{
    private readonly IAppUpdateService service;
    private readonly OperationLifetime lifetime;
    private readonly Func<bool> canRestart;
    private readonly Version installedVersion;
    private bool working;
    private bool downloading;
    private bool restarting;
    private string status = "Comprueba si hay una nueva versión.";
    private string error = "";
    private double progressPercent;
    private PreparedAppUpdate? preparedUpdate;

    internal AppUpdateViewModel(IAppUpdateService service, OperationLifetime lifetime, Func<bool> canRestart, Version installedVersion)
    {
        this.service = service;
        this.lifetime = lifetime;
        this.canRestart = canRestart;
        this.installedVersion = installedVersion;
        CheckCommand = new AsyncCommand(_ => CheckAsync(), _ => CanCheck, ex => ShowError(ex));
        CancelCommand = new RelayCommand(_ => { if (IsWorking) lifetime.Cancel(); }, _ => IsWorking && lifetime.CanCancel);
        RestartCommand = new RelayCommand(_ => RequestRestart(), _ => CanRestart);
    }

    public string InstalledVersion => FormatVersion(installedVersion);
    public string Status { get => status; private set => Set(ref status, value); }
    public string Error { get => error; private set { if (Set(ref error, value)) Changed(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool IsWorking { get => working; private set { if (Set(ref working, value)) { Changed(nameof(IsChecking)); RefreshAvailability(); } } }
    public bool IsDownloading { get => downloading; private set { if (Set(ref downloading, value)) Changed(nameof(IsChecking)); } }
    public bool IsChecking => IsWorking && !IsDownloading;
    public bool IsRestarting { get => restarting; private set { if (Set(ref restarting, value)) RefreshAvailability(); } }
    public double ProgressPercent { get => progressPercent; private set { if (Set(ref progressPercent, value)) Changed(nameof(ProgressLabel)); } }
    public string ProgressLabel => $"{ProgressPercent:N0}% descargado";
    public PreparedAppUpdate? PreparedUpdate => preparedUpdate;
    public bool IsReady => preparedUpdate is not null;
    public bool CanCheck => !IsWorking && !IsRestarting && !lifetime.IsStopping;
    public bool CanRestart => IsReady && CanCheck && canRestart();
    public string CheckLabel => HasError ? "Reintentar búsqueda" : "Buscar actualización";
    public ICommand CheckCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RestartCommand { get; }
    public event Action? RestartRequested;

    public async Task CheckAsync()
    {
        if (!CanCheck) return;
        IsWorking = true; Error = ""; ProgressPercent = 0;
        preparedUpdate = null; Changed(nameof(IsReady)); Changed(nameof(PreparedUpdate));
        Status = "Buscando nueva versión…";
        try
        {
            await lifetime.RunAsync(async token =>
            {
                var release = await service.CheckAsync(installedVersion, token);
                token.ThrowIfCancellationRequested();
                if (release is null) { Status = "Tu versión está actualizada."; return; }
                Status = $"Descargando versión {FormatVersion(release.Version)}…";
                IsDownloading = true;
                var progress = new Progress<AppUpdateProgress>(value =>
                {
                    if (IsDownloading && !token.IsCancellationRequested)
                        ProgressPercent = value.Total > 0 ? Math.Clamp(100d * value.Received / value.Total, 0, 100) : 0;
                });
                var update = await service.DownloadAsync(release, progress, token);
                token.ThrowIfCancellationRequested();
                preparedUpdate = update;
                ProgressPercent = 100;
                Changed(nameof(PreparedUpdate)); Changed(nameof(IsReady));
                Status = $"Versión {FormatVersion(release.Version)} lista para instalar.";
            });
        }
        catch (OperationCanceledException) { Status = "Descarga cancelada. Puedes volver a buscar."; }
        catch (Exception ex) { ShowError(ex); }
        finally { IsDownloading = false; IsWorking = false; }
    }

    private void RequestRestart()
    {
        if (!CanRestart || RestartRequested is null) return;
        Error = ""; IsRestarting = true;
        try { RestartRequested.Invoke(); }
        catch (Exception ex) { RestartFailed(ex); }
    }

    internal void RestartFailed(Exception exception)
    {
        IsRestarting = false;
        Error = $"No se pudo iniciar la actualización. {exception.Message}";
        Status = "Actualización lista. Puedes reintentar el reinicio.";
        Changed(nameof(CheckLabel));
    }

    private void ShowError(Exception exception)
    {
        Error = $"No se pudo obtener la actualización. {exception.Message}";
        Status = "Actualización no disponible. Reintenta la búsqueda.";
        Changed(nameof(CheckLabel));
    }

    internal void RefreshAvailability()
    {
        Changed(nameof(CanCheck)); Changed(nameof(CanRestart)); Changed(nameof(CheckLabel));
        CommandManager.InvalidateRequerySuggested();
    }

    private static string FormatVersion(Version version) => version.Revision > 0 ? version.ToString(4) : version.ToString(3);
}
