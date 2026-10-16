using System.Collections.ObjectModel;
using System.Windows.Input;
using Silen.Core;

namespace Silen.App;

public sealed record BloomMessage(string Role, string Text)
{
    public bool IsUser => Role == "Tú";
}

public sealed class BloomViewModel : ObservableObject
{
    private readonly IBloomSession session;
    private readonly Func<string, Func<CancellationToken, Task>, Task<bool>> run;
    private readonly Func<bool> canRun;
    private readonly Func<CancellationToken, Task> refresh;
    private BloomState state = BloomState.Idle;
    private BloomProvider selectedProvider;
    private string selectedModel = BloomConfiguration.Default.Model;
    private bool loaded, hasKey, responding;
    private string input = "", status = "Tu clave se cifra para este usuario de Windows. La conexión se comprueba al enviar el primer mensaje.";
    internal BloomViewModel(IBloomSession session,
        Func<string, Func<CancellationToken, Task>, Task<bool>> run, Func<bool> canRun,
        Func<CancellationToken, Task> refresh, Action<string> openKeyPage)
    {
        this.session = session; this.run = run; this.canRun = canRun; this.refresh = refresh;
        session.MessageAdded += OnMessageAdded;
        ConfigureCommand = Command(_ => ConfigureAsync(), () => !HasPending);
        SendCommand = Command(_ => SendAsync(), () => HasKey && !HasPending && !string.IsNullOrWhiteSpace(Input));
        ConfirmCommand = Command(_ => ConfirmAsync(), () => HasPending);
        CancelProposalCommand = Command(_ => CancelProposalAsync(), () => HasPending);
        DeleteKeyCommand = Command(_ => DeleteKeyAsync());
        NewConversationCommand = Command(_ => NewConversationAsync(), () => HasKey && !HasPending);
        OpenKeyPageCommand = new RelayCommand(_ =>
        {
            var keyPage = new BloomConfiguration(SelectedProvider, SelectedModel).KeyPage;
            try { openKeyPage(keyPage); } catch { SetStatus("No se pudo abrir el navegador. Visita " + keyPage); }
        });
    }
    public ObservableCollection<BloomMessage> Messages { get; } = [];
    public ICommand SendCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand CancelProposalCommand { get; }
    public ICommand DeleteKeyCommand { get; }
    public ICommand NewConversationCommand { get; }
    public ICommand OpenKeyPageCommand { get; }
    public ICommand ConfigureCommand { get; }
    public IReadOnlyList<BloomProvider> Providers { get; } = Enum.GetValues<BloomProvider>();
    public BloomProvider SelectedProvider
    {
        get => selectedProvider;
        set { if (Set(ref selectedProvider, value)) { SelectedModel = BloomConfiguration.DefaultModel(value); Changed(nameof(Models)); } }
    }
    public IReadOnlyList<string> Models => BloomConfiguration.Models(SelectedProvider);
    public string SelectedModel { get => selectedModel; set => Set(ref selectedModel, value); }
    public BloomState State => state;
    public string StateLabel => state switch
    {
        BloomState.Idle => "En reposo", BloomState.Analyzing => "Analizando", BloomState.Thinking => "Pensando",
        BloomState.Success => "Completado", BloomState.Warning => "Requiere atención", _ => "Error"
    };
    public string UsageSummary => $"Tokens informados: {session.Usage.InputTokens:N0} de entrada y {session.Usage.OutputTokens:N0} de salida. Límite local: {BloomLimits.SessionTokens:N0} por conversación. Puede haber cargos del proveedor.";
    public string PrivacySummary => "Se envían tus mensajes al proveedor elegido. Cada diagnóstico requiere autorización local y comparte solo métricas agregadas; excluye identificadores, procesos y comandos. El chat no se guarda en disco.";
    public bool HasKey => hasKey;
    public string ModelLabel => session.ModelLabel;
    public bool NeedsKey => !hasKey;
    public bool HasMessages => Messages.Count > 0;
    public bool IsResponding => responding;
    public bool HasError => state == BloomState.Error;
    public bool HasPending => session.HasPending;
    public string PendingDescription => session.PendingDescription;
    public bool CanInteract => canRun();
    public string Input { get => input; set { if (Set(ref input, value)) CommandManager.InvalidateRequerySuggested(); } }
    public string Status => status;
    internal void LoadConfiguration()
    {
        if (loaded) return;
        loaded = true;
        try { session.LoadConfiguration(); hasKey = session.HasKey; selectedProvider = session.Configuration.Provider; selectedModel = session.Configuration.Model; }
        catch (BloomException ex) { state = BloomState.Error; SetStatus(ex.Message); }
        catch { SetStatus("No se pudo leer la configuración local de Bloom."); }
        RefreshAvailability();
    }
    internal Task<bool> SaveKeyAsync(string key) => ExecuteAsync("Guardando la clave cifrada de Bloom…", _ =>
    {
        if (HasPending) throw new BloomException("Confirma o cancela la propuesta pendiente antes de guardar otra clave.");
        var selected = new BloomConfiguration(SelectedProvider, SelectedModel); selected.Validate();
        if (selected != session.Configuration) session.Configure(selected);
        session.SaveKey(key.Trim()); hasKey = true; loaded = true; ResetConversation();
        SetStatus("Clave guardada con cifrado de Windows. Envía un mensaje para comprobar la conexión.");
        return Task.CompletedTask;
    });
    internal Task<bool> DeleteKeyAsync() => ExecuteAsync("Eliminando la clave local de Bloom…", _ =>
    {
        session.DeleteKey(); hasKey = false; Input = ""; ResetConversation(); Messages.Clear();
        SetStatus("Clave local eliminada. Puedes guardar otra cuando quieras."); return Task.CompletedTask;
    });
    internal Task<bool> NewConversationAsync() => ExecuteAsync("Iniciando una conversación…", _ =>
    {
        ResetConversation(); Messages.Clear(); Input = "";
        SetStatus("Conversación nueva. No se guarda el chat en disco."); return Task.CompletedTask;
    });
    internal Task<bool> ConfigureAsync() => ExecuteAsync("Configurando Bloom…", _ =>
    {
        session.Configure(new(SelectedProvider, SelectedModel)); hasKey = session.HasKey;
        Messages.Clear(); Input = ""; loaded = true;
        SetStatus("Proveedor y modelo seleccionados. Cada proveedor usa su propia clave cifrada.");
        return Task.CompletedTask;
    });
    internal Task<bool> SendAsync() => ExecuteAsync("Bloom está respondiendo…", async token =>
    {
        var text = Input; Input = "";
        await session.SendAsync(text, token);
        SetStatus(HasPending ? "Revisa los pasos antes de confirmar." : "Respuesta recibida.");
    });
    internal Task<bool> ConfirmAsync() => ExecuteAsync("Ejecutando la propuesta confirmada…", async token =>
    {
        state = BloomState.Analyzing; RefreshAvailability();
        try { await session.ConfirmAsync(token); }
        finally { await refresh(CancellationToken.None); }
        SetStatus(HasPending ? "Hay otra propuesta que necesita su propia confirmación." : "Consulta el resultado en el chat. Los cambios respaldados están en Ajustes.");
    });
    internal Task<bool> CancelProposalAsync() => ExecuteAsync("Cancelando la propuesta…", async token =>
    { await session.CancelAsync(token); SetStatus("Propuesta cancelada."); });
    internal void RefreshAvailability() { Changed(null); CommandManager.InvalidateRequerySuggested(); }
    private void ResetConversation() => session.Reset();
    private void OnMessageAdded(string role, string text)
    {
        if (session.HasPending && text == session.PendingDescription)
            text = text.EndsWith("¿Deseas que aplique estos ajustes?", StringComparison.Ordinal)
                ? "Hay una propuesta lista para revisar abajo. ¿Deseas que aplique estos ajustes?"
                : "Hay un resumen de diagnóstico listo para autorizar abajo. ¿Autorizas recopilarlo y enviarlo al proveedor seleccionado?";
        Messages.Add(new(role, text)); RefreshAvailability();
    }
    private AsyncCommand Command(Func<object?, Task> action, Func<bool>? predicate = null) =>
        new(action, _ => canRun() && (predicate?.Invoke() ?? true), _ => SetStatus("Bloom no pudo completar la acción. Revisa el estado antes de volver a intentarlo."));
    private Task<bool> ExecuteAsync(string label, Func<CancellationToken, Task> action) => run(label, async token =>
    {
        responding = true; state = BloomState.Thinking; RefreshAvailability();
        try { await action(token); state = HasPending ? BloomState.Warning : BloomState.Success; }
        catch (OperationCanceledException) { state = BloomState.Warning; SetStatus("Operación cancelada. No se repetirán herramientas. Revisa los resultados y respaldos si se inició algún cambio."); }
        catch (BloomException ex) { state = BloomState.Error; SetStatus(ex.Message); }
        catch { state = BloomState.Error; SetStatus("No se pudo completar la acción de Bloom. Revisa el resultado y los respaldos antes de volver a intentarlo."); }
        finally { responding = false; RefreshAvailability(); }
    });
    private void SetStatus(string value) { status = value; Changed(nameof(Status)); }
}

