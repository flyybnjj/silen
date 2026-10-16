using Silen.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Silen.Windows;

public enum SystemActionKind { SetToggle, ResumeWindowsUpdate, Undo, InspectHistory, DownloadDrivers, CreateRestorePoint }
public sealed record SystemDriverUpdateIdentity(Guid UpdateId, int Revision);
public sealed record SystemActionRequest(SystemActionKind Action, string? TargetId = null, bool? Enabled = null, IReadOnlyList<SystemDriverUpdateIdentity>? Updates = null);
public sealed record SystemActionResponse(OptimizationOutcome Outcome, OptimizationOverview? Overview = null, DriverDownloadResult? Download = null, RestorePointResult? RestorePoint = null);

public static class SystemActionPolicy
{
    public const int MaximumRequestBytes = 2048;
    public const int MaximumResponseBytes = 1024 * 1024;
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(2);
    internal static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public static bool IsPrivilegedToggle(string? id) => id is "delivery-optimization" or "power-throttling" or
        "gaming-network-bandwidth" or "service-diagtrack" or "service-maps" or "service-fax";
    public static SystemActionRequest Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is 0 or > MaximumRequestBytes) throw new ArgumentException("Solicitud de sistema fuera del límite.");
        try
        {
            using var doc = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Solicitud incompatible.");
            RejectDuplicateProperties(doc.RootElement);
            if (!doc.RootElement.TryGetProperty("Action", out _)) throw new ArgumentException("Falta la operación autorizada.");
            var request = JsonSerializer.Deserialize<SystemActionRequest>(bytes, Json) ?? throw new ArgumentException("Solicitud vacía.");
            Validate(request);
            return request;
        }
        catch (JsonException ex) { throw new ArgumentException("Solicitud de sistema incompatible.", ex); }
    }
    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new ArgumentException("La solicitud contiene propiedades duplicadas.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }
    public static byte[] Serialize(SystemActionRequest request)
    {
        Validate(request);
        return JsonSerializer.SerializeToUtf8Bytes(request, Json);
    }
    public static void Validate(SystemActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var valid = request.Action switch
        {
            SystemActionKind.SetToggle => IsPrivilegedToggle(request.TargetId) && request.Enabled is not null && request.Updates is null,
            SystemActionKind.ResumeWindowsUpdate or SystemActionKind.InspectHistory or SystemActionKind.CreateRestorePoint => request.TargetId is null && request.Enabled is null && request.Updates is null,
            SystemActionKind.Undo => Guid.TryParseExact(request.TargetId, "N", out _) && request.Enabled is null && request.Updates is null,
            SystemActionKind.DownloadDrivers => request.TargetId is null && request.Enabled is null && request.Updates is { Count: > 0 and <= 20 } updates &&
                updates.All(update => update is not null && update.UpdateId != Guid.Empty && update.Revision is >= 1 and <= 1000000) &&
                updates.Select(update => update.UpdateId).Distinct().Count() == updates.Count,
            _ => false
        };
        if (!valid) throw new ArgumentException("Operación fuera del catálogo autorizado.");
    }
    public static void ValidatePeer(string expectedSid, string actualSid, int expectedSession, int actualSession, bool remote)
    {
        if (remote || string.IsNullOrWhiteSpace(expectedSid) || !string.Equals(expectedSid, actualSid, StringComparison.Ordinal) || expectedSession != actualSession)
            throw new UnauthorizedAccessException("El ejecutor requiere el mismo usuario y la misma sesión local.");
    }
}

internal enum OptimizationAuthority { User, Machine, TestOnly }
