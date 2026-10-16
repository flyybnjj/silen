namespace Silen.Windows;



internal static class PerformanceTweaks
{
    internal sealed record Value(string Key, string Name, int Target);
    internal sealed record Group(string Id, string Title, string Description, Value[] Values,
        string Category = "Segundo plano", bool Recommended = false, bool RequiresAc = false);
    private const string Content = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string Edge = @"Software\Policies\Microsoft\Edge";
    internal static readonly Group[] Groups =
    [
        new("gamebar-background", "Reducir funciones de grabación de Game Bar", "Desactiva captura de audio/cursor, apertura con el botón del mando y panel inicial de Game Bar. No afecta OBS ni elimina Xbox.",
            [new(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AudioCaptureEnabled", 0), new(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "CursorCaptureEnabled", 0), new(@"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0), new(@"Software\Microsoft\GameBar", "ShowStartupPanel", 0)]),
        new("desktop-composition", "Reducir efectos del escritorio", "Desactiva transparencias y Aero Peek para reducir efectos de composición. Puede requerir volver a iniciar sesión; no cambia los gráficos de los juegos.",
            [new(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0), new(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0)]),
        new("background-apps", "Reducir apps en segundo plano", "Establece la preferencia de acceso en segundo plano de apps Store; puede limitar notificaciones y sincronización. Su efecto depende de la versión de Windows.",
            [new(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1)]),
        new("browser-background", "Reducir procesos de Edge al cerrarlo", "Desactiva Startup Boost y el modo de segundo plano. Afecta precarga, extensiones y apps web cuando el navegador está cerrado; requiere reiniciar Edge.",
            [new(Edge, "StartupBoostEnabled", 0), new(Edge, "BackgroundModeEnabled", 0)], Recommended: true),
        new("chrome-background", "Reducir procesos de Chrome al cerrarlo", "Desactiva el modo de segundo plano al cerrar la última ventana de Chrome. Puede limitar apps web y extensiones; no cierra tus pestañas ni procesos actuales. La política impide cambiar esta opción en Chrome hasta restaurarla. Algunas tareas ya iniciadas pueden continuar.",
            [new(@"Software\Policies\Google\Chrome", "BackgroundModeEnabled", 0)]),
        new("content-suggestions", "Reducir contenido promocional", "Desactiva preferencias de instalaciones silenciosas y recomendaciones; no desinstala apps ni altera Windows Update.",
            [new(Content, "SilentInstalledAppsEnabled", 0), new(Content, "SystemPaneSuggestionsEnabled", 0), new(Content, "SubscribedContent-338388Enabled", 0), new(Content, "SubscribedContent-338389Enabled", 0), new(Content, "SubscribedContent-353694Enabled", 0)]),
        new("diagnostic-personalization", "Reducir personalización diagnóstica", "Desactiva preferencias de publicidad, experiencias personalizadas y recopilación para personalización de escritura. No equivale a desactivar toda la telemetría de Windows.",
            [new(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0), new(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0), new(@"Software\Microsoft\Input\TIPC", "Enabled", 0), new(@"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1), new(@"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1), new(@"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0)]),
        new("online-search", "Reducir búsqueda web del escritorio", "Establece preferencias para reducir Bing y sugerencias web en la búsqueda del escritorio. La disponibilidad efectiva depende de Windows y sus políticas.",
            [new(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0), new(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1)]),
        new("delivery-optimization", "Evitar compartir actualizaciones con otros equipos", "Usa descargas HTTP sin intercambio entre equipos. Puede reducir tráfico de subida; conserva Windows Update y Microsoft Store. Requiere administrador. Una política de tu organización puede prevalecer.",
            [new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0)], "Red"),
        new("power-throttling", "Desactivar Power Throttling con corriente", "Desactiva la reducción de CPU para procesos elegibles en segundo plano. No cambia los límites térmicos. Puede aumentar consumo, ruido y temperatura; la política sigue activa al desconectar hasta restaurarla. Requiere administrador y volver a abrir las apps.",
            [new(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1)], "Energía", RequiresAc: true),
        new("gaming-network-bandwidth", "Limitar descargas de actualizaciones en segundo plano", "Limita Delivery Optimization al 10 % del ancho de banda estimado para descargas en segundo plano. Puede reducir congestión al jugar; no garantiza menor ping ni limita otros launchers. Requiere administrador y puede retrasar descargas de Windows Update o Store.",
            [new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DOPercentageMaxBackgroundBandwidth", 10)], "Red para jugar")
    ];
    internal static IEnumerable<Group> ProfileGroups => Groups.Where(group => group.Id is "gamebar-background" or "desktop-composition" or "background-apps" or "browser-background");
    internal static Group Find(string id) => Groups.FirstOrDefault(group => group.Id == id)
        ?? throw new ArgumentException("Esta optimización no está disponible.");
    internal static bool Allows(string id, OptimizationValueChange value) => Groups.Any(group => group.Id == id &&
        group.Values.Any(target => value.Kind == "Registry" && value.Key == target.Key && value.Name == target.Name &&
            value.Intended.Same(RegistryValueState.Dword(target.Target))));
}
