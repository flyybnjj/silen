using System.Text.Json;

namespace Silen.Windows;

/// <summary>Acuse independiente y páginas de lectura en la misma sesión autorizada.</summary>
internal static class SystemActionConversation
{
    internal static async Task SendAsync(Stream connection, SystemActionResponse acknowledgement,
        Func<CancellationToken, IAsyncEnumerable<SystemActionHistoryPage>>? history, CancellationToken token)
    {
        // No inspeccionar ni serializar historial antes de guardar este acuse en el canal.
        await WriteAsync(connection, acknowledgement with { Overview = null, HistoryFollows = history is not null }, token);
        if (history is null) return;
        var next = 0;
        try
        {
            await foreach (var page in history(token).WithCancellation(token))
            {
                ValidatePage(page, next);
                await WriteAsync(connection, page, token);
                next++;
                if (!page.HasMore) return;
            }
            throw new InvalidDataException("Historial incompleto.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException or OperationCanceledException)
        {
            // El acuse previo sigue siendo válido aunque la lectura falle después.
            await WriteAsync(connection, new SystemActionHistoryPage(next, [], false,
                Error: "No se pudo actualizar el historial. Los respaldos se conservan; vuelve a consultarlos antes de reintentar."), token);
        }
    }

    internal static async Task<SystemActionResponse> ReceiveAsync(Stream connection, CancellationToken token)
    {
        var header = await SystemActionHost.ReadMessageAsync(connection, SystemActionPolicy.MaximumResponseBytes, token);
        var acknowledgement = JsonSerializer.Deserialize<SystemActionResponse>(header, SystemActionPolicy.Json)
            ?? throw new InvalidDataException("Falta el acuse de la operación.");
        if (acknowledgement.Outcome is null || acknowledgement.Overview is not null)
            throw new InvalidDataException("Acuse de operación incompatible.");
        if (!acknowledgement.HistoryFollows) return acknowledgement;
        var changes = new List<AppliedOptimization>();
        List<ToggleSetting> toggles = [];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;
        try
        {
            for (var next = 0; next < SystemActionPolicy.MaximumHistoryPages; next++)
            {
                var bytes = await SystemActionHost.ReadMessageAsync(connection, SystemActionPolicy.MaximumResponseBytes, token);
                total = checked(total + bytes.Length);
                if (total > SystemActionPolicy.MaximumHistoryBytes) throw new InvalidDataException("Historial fuera del límite.");
                var page = JsonSerializer.Deserialize<SystemActionHistoryPage>(bytes, SystemActionPolicy.Json)
                    ?? throw new InvalidDataException("Página de historial vacía.");
                ValidatePage(page, next);
                if (page.Error is not null) throw new InvalidDataException(page.Error);
                if (page.Toggles is not null) toggles = page.Toggles;
                foreach (var change in page.Changes)
                    if (!ids.Add(change.Id)) throw new InvalidDataException("El historial contiene identificadores duplicados.");
                changes.AddRange(page.Changes);
                if (!page.HasMore)
                    return acknowledgement with { HistoryFollows = false, Overview = new(toggles, [], [], changes) };
            }
            throw new InvalidDataException("Historial fuera del límite de páginas.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or OperationCanceledException)
        {
            const string warning = "No se pudo actualizar el historial completo. Los respaldos se conservan; vuelve a consultarlos antes de reintentar.";
            return acknowledgement with { HistoryFollows = false, HistoryWarning = warning,
                Outcome = acknowledgement.Outcome with { Message = acknowledgement.Outcome.Message + " " + warning } };
        }
    }

    private static Task WriteAsync<T>(Stream connection, T value, CancellationToken token) =>
        SystemActionHost.WriteMessageAsync(connection, JsonSerializer.SerializeToUtf8Bytes(value, SystemActionPolicy.Json),
            SystemActionPolicy.MaximumResponseBytes, token);

    private static void ValidatePage(SystemActionHistoryPage page, int number)
    {
        if (page.Number != number || number >= SystemActionPolicy.MaximumHistoryPages || page.Changes is null ||
            page.Changes.Count > SystemActionPolicy.HistoryPageSize || page.HasMore && page.Changes.Count == 0 ||
            page.Error is not null && (page.HasMore || page.Changes.Count != 0 || page.Error.Length > 512) ||
            page.Toggles is not null && (number != 0 || page.Toggles.Count > 16 || page.Toggles.Any(toggle => toggle is null || !SystemActionPolicy.IsPrivilegedToggle(toggle.Id))) ||
            page.Changes.Any(change => change is null || !Guid.TryParseExact(change.Id, "N", out _) ||
                string.IsNullOrWhiteSpace(change.Title) || change.Title.Length > 256 || change.Detail is null || change.Detail.Length > 1024))
            throw new InvalidDataException("Página de historial incompatible.");
    }
}
