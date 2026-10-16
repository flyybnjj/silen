using System.Runtime.CompilerServices;

namespace Silen.Windows;

public sealed partial class NativeOptimizationService
{
    // Una instantánea bajo el mismo lease: ninguna página cambia de orden por
    // escrituras concurrentes. Se verifican únicamente los valores de cada página.
    internal async IAsyncEnumerable<SystemActionHistoryPage> ReadHistoryPagesAsync(
        [EnumeratorCancellation] CancellationToken token)
    {
        await using var lease = await journal.LeaseAsync(token);
        var entries = (await journal.ReadAsync(token)).OrderByDescending(entry => entry.AppliedAt)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal).ToArray();
        var toggles = InspectPreferences(entries.ToList()).Where(toggle => SystemActionPolicy.IsPrivilegedToggle(toggle.Id))
            .Select(toggle => toggle with { Status = LimitHistoryText(toggle.Status, 1024) }).ToList();
        var page = 0;
        do
        {
            token.ThrowIfCancellationRequested();
            var offset = page * SystemActionPolicy.HistoryPageSize;
            var changes = entries.Skip(offset).Take(SystemActionPolicy.HistoryPageSize)
                .Select(entry => HistoryItem(entry) with { Title = LimitHistoryText(entry.Title, 256) }).ToList();
            yield return new(page, changes, offset + changes.Count < entries.Length, page == 0 ? toggles : null);
            page++;
        } while (page * SystemActionPolicy.HistoryPageSize < entries.Length);
    }

    private AppliedOptimization HistoryItem(OptimizationJournalEntry entry) => new(entry.Id, entry.Title,
        entry.AppliedAt, CanUndo(entry), entry.State switch
        {
            "Completed" => "Aplicado. Se guardó el estado anterior exacto.",
            "Undone" => "Se restauró el estado anterior.",
            "Pending" => "Operación interrumpida; el respaldo permite revisar y recuperar los valores aplicados.",
            "UndoPending" => "Restauración interrumpida; se puede volver a intentar.",
            _ => "Aplicación parcial; el respaldo conserva los valores originales."
        }, entry.State == "Undone");

    private static string LimitHistoryText(string text, int maximum) => text.Length <= maximum ? text : text[..(maximum - 1)] + "…";
}
