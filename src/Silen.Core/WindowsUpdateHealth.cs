namespace Silen.Core;

public sealed record WindowsUpdateHealth(bool Known, bool Paused, DateTimeOffset? PausedUntil,
    bool? ServiceEnabled, bool? ServiceRunning, bool Managed, string Message, bool PauseManaged = false)
{
    public bool CanSearch => Known && !Paused && ServiceEnabled == true && ServiceRunning == true;
    public bool NeedsRecovery => Known && !PauseManaged && (Paused || ServiceEnabled == false || ServiceRunning == false);
    public static WindowsUpdateHealth Unknown(string message) => new(false, false, null, null, null, false, message);
}
