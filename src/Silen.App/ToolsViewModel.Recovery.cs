using System.Collections.ObjectModel;
using System.Windows.Input;
using Silen.Windows;
using Silen.Core;

namespace Silen.App;

public sealed class RecoveryChoice(CleanupRecoveryItem file) : ObservableObject
{
    private bool selected;
    public CleanupRecoveryItem File { get; } = file;
    public bool Selected { get => selected; set { if (Set(ref selected, value)) CommandManager.InvalidateRequerySuggested(); } }
}
public sealed partial class ToolsViewModel
{
    public ObservableCollection<RecoveryChoice> RecoveryFiles { get; } = [];
    public bool HasRecoveryFiles => RecoveryFiles.Count > 0;
    public string RecoverySummary => $"{SpanishText.Count(RecoveryFiles.Count, "archivo conservado", "archivos conservados")} · {Size(RecoveryFiles.Sum(item => item.File.Bytes))}. No se ha liberado ese espacio.";
    public ICommand RestoreRecoveryCommand { get; private set; } = null!;
    public ICommand PurgeRecoveryCommand { get; private set; } = null!;
    private void InitializeRecovery()
    {
        RestoreRecoveryCommand = Command(_ => ChangeRecoveryAsync(false), _ => RecoveryFiles.Any(item => item.Selected));
        PurgeRecoveryCommand = Command(_ => ChangeRecoveryAsync(true), _ => RecoveryFiles.Any(item => item.Selected));
    }
    private async Task RefreshRecoveryAsync(CancellationToken token)
    {
        var selected = RecoveryFiles.Where(item => item.Selected).Select(item => item.File.Id).ToHashSet(StringComparer.Ordinal);
        var files = await service.ListRecoveryAsync(token);
        RecoveryFiles.Clear(); foreach (var file in files) RecoveryFiles.Add(new(file) { Selected = selected.Contains(file.Id) });
        Changed(nameof(HasRecoveryFiles)); Changed(nameof(RecoverySummary));
    }
    private Task<bool> ChangeRecoveryAsync(bool purge)
    {
        var selected = RecoveryFiles.Where(item => item.Selected).Select(item => item.File).ToArray();
        var detail = string.Join("\n", selected.Take(20).Select(file => file.RelativePath)) + (selected.Length > 20 ? $"\n… y {selected.Length - 20} más." : "");
        if (selected.Length == 0 || !confirmManual(purge ? "Eliminar definitivamente" : "Restaurar temporales",
            $"{SpanishText.Count(selected.Length, "archivo", "archivos")} ({Size(selected.Sum(item => item.Bytes))}). " + (purge ? "La purga libera espacio y no tiene deshacer." : "Restauración a la ubicación original, sin sobrescribir archivos nuevos.") + "\n\n" + detail)) return Task.FromResult(false);
        return run(purge ? "Purgando archivos confirmados…" : "Restaurando temporales…", async token =>
        {
            try
            {
                var ids = selected.Select(item => item.Id).ToArray();
                var result = purge ? await service.PurgeRecoveryAsync(ids, token) : await service.RestoreRecoveryAsync(ids, token);
                CleanupStatus = result.Message;
            }
            finally { await RefreshRecoveryAsync(CancellationToken.None); }
        });
    }
}
