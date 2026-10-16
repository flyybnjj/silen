namespace Silen.Core;

/// <summary>La interfaz de usuario solo recibe estado de credenciales, nunca su valor almacenado.</summary>
public interface IBloomSession
{
    event Action<string, string>? MessageAdded;
    BloomConfiguration Configuration { get; }
    BloomUsage Usage { get; }
    bool HasKey { get; }
    bool HasPending { get; }
    string PendingDescription { get; }
    string ModelLabel { get; }
    void LoadConfiguration();
    void Configure(BloomConfiguration configuration);
    void SaveKey(string key);
    void DeleteKey();
    void Reset();
    Task SendAsync(string text, CancellationToken token = default);
    Task ConfirmAsync(CancellationToken token = default);
    Task CancelAsync(CancellationToken token = default);
}

public sealed class BloomSession : IBloomSession
{
    private readonly Func<BloomProvider, IBloomKeyStore> keyFactory;
    private readonly Func<BloomConfiguration, IBloomClient> clientFactory;
    private readonly IBloomTools tools;
    private readonly BloomConfigurationStore? settings;
    private BloomConversation conversation = null!;
    private IBloomClient client = null!;
    private bool hasKey;
    public BloomSession(IBloomTools tools, Func<BloomProvider, IBloomKeyStore> keys,
        Func<BloomConfiguration, IBloomClient>? clients = null, BloomConfigurationStore? settings = null)
    {
        this.tools = tools; keyFactory = keys; clientFactory = clients ?? (configuration => new BloomProviderClient(configuration));
        this.settings = settings; Reset();
    }
    public BloomSession(IBloomKeyStore keys, IBloomClient client, IBloomTools tools)
        : this(tools, _ => keys, _ => client) { }
    public event Action<string, string>? MessageAdded;
    public BloomConfiguration Configuration { get; private set; } = BloomConfiguration.Default;
    public BloomUsage Usage => conversation.Usage;
    public bool HasKey => hasKey;
    public bool HasPending => conversation.HasPending;
    public string PendingDescription => conversation.PendingDescription;
    public string ModelLabel => client.ModelLabel;
    public void LoadConfiguration()
    {
        Configuration = settings?.Load() ?? Configuration;
        hasKey = false; Reset(); hasKey = keyFactory(Configuration.Provider).Read() is not null;
    }
    public void Configure(BloomConfiguration configuration)
    {
        if (HasPending) throw new BloomException("Cancela la propuesta antes de cambiar de proveedor.");
        configuration.Validate(); settings?.Save(configuration); Configuration = configuration;
        hasKey = false; Reset(); hasKey = keyFactory(configuration.Provider).Read() is not null;
    }
    public void SaveKey(string key) { keyFactory(Configuration.Provider).Save(key); hasKey = true; Reset(); }
    public void DeleteKey() { keyFactory(Configuration.Provider).Delete(); hasKey = false; Reset(); }
    public void Reset()
    {
        client = clientFactory(Configuration);
        conversation = new(client, tools);
        conversation.MessageAdded += (role, text) => MessageAdded?.Invoke(role, text);
    }
    private string ReadKey() => keyFactory(Configuration.Provider).Read() ?? throw new BloomException("La clave local ya no está disponible. Vuelve a guardarla.");
    public Task SendAsync(string text, CancellationToken token = default) => conversation.SendAsync(ReadKey(), text, token);
    public Task ConfirmAsync(CancellationToken token = default) => conversation.ConfirmAsync(ReadKey(), token);
    public Task CancelAsync(CancellationToken token = default) => conversation.CancelAsync("", token);
}
