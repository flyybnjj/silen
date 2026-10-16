using System.Globalization;

namespace Silen.Windows;

public enum RestorePointState { Created, Reused, Disabled, Unavailable, Failed, Cancelled }
public sealed record RestorePointResult(RestorePointState State, long? SequenceNumber = null, DateTimeOffset? CreatedAt = null, int? ErrorCode = null)
{
    public bool Verified => SequenceNumber is > 0 && CreatedAt is not null;
    public bool CanContinueAutomatically => State == RestorePointState.Created && Verified;
    private string Identity => Verified ? $"n.º {SequenceNumber}, del {CreatedAt!.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-CL"))}" : "sin identificador y fecha verificados";
    public string Message => State switch
    {
        RestorePointState.Created when Verified => $"Punto de restauración creado y verificado: {Identity}.",
        RestorePointState.Reused when Verified => $"Windows reutilizó un punto de restauración ({Identity}). No creó uno nuevo para este lote.",
        RestorePointState.Disabled => "Windows no pudo crear el punto de restauración: Protección del sistema está desactivada.",
        RestorePointState.Cancelled => "Solicitud del punto de restauración cancelada. No se iniciaron ajustes. Una creación nativa ya iniciada puede terminar y conservarse.",
        RestorePointState.Unavailable => "No se pudo crear y verificar el punto de restauración: la función o su consulta no están disponibles.",
        _ => $"No se pudo crear y verificar un punto de restauración nuevo.{(ErrorCode is { } code ? $" Windows informó el código {code}." : "") }"
    };
}
public interface IRestorePointService
{
    Task<RestorePointResult> PrepareAsync(CancellationToken cancellationToken = default);
}

/// <summary>Solo solicita la operación tipada al ayudante; no eleva la interfaz.</summary>
public sealed class SystemRestoreClient(Func<SystemActionRequest, CancellationToken, Task<SystemActionResponse>> execute) : IRestorePointService
{
    public async Task<RestorePointResult> PrepareAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(RestorePointState.Cancelled);
        try
        {
            var response = await execute(new(SystemActionKind.CreateRestorePoint), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var point = response.RestorePoint ?? new(RestorePointState.Failed);
            return point.State == RestorePointState.Created && !response.Outcome.Success ? new(RestorePointState.Failed) : point;
        }
        catch (OperationCanceledException) { return new(RestorePointState.Cancelled); }
    }
}
