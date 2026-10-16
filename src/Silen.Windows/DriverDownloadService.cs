using System.Runtime.InteropServices;
using Silen.Core;

namespace Silen.Windows;


public sealed class DriverDownloadService
{
    private static readonly SemaphoreSlim ActiveDownload = new(1, 1);
    private readonly Func<IReadOnlyList<DriverUpdateInfo>, CancellationToken, DriverDownloadResult> workerAction;
    private readonly TimeSpan limit;
    public DriverDownloadService() : this(Download, TimeSpan.FromMinutes(20)) { }
    internal DriverDownloadService(Func<IReadOnlyList<DriverUpdateInfo>, CancellationToken, DriverDownloadResult> workerAction, TimeSpan limit)
    { this.workerAction = workerAction; this.limit = limit; }
    public async Task<DriverDownloadResult> DownloadAsync(IReadOnlyList<DriverUpdateInfo> offers,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (offers.Count is 0 or > 1000 || offers.Any(offer => !Guid.TryParse(offer.UpdateId, out _) || offer.Revision < 1) ||
            offers.Select(offer => offer.UpdateId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != offers.Count)
            return new(false, [], "Vuelve a buscar actualizaciones antes de descargar.");
        if (!await ActiveDownload.WaitAsync(0, cancellationToken))
            return new(false, [], "Windows sigue finalizando la descarga anterior. Espera un momento antes de reintentar.");
        var workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var worker = Task.Factory.StartNew(() =>
        {
            try { return workerAction(offers, workerCancellation.Token); }
            finally { ActiveDownload.Release(); }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try { return await worker.WaitAsync(limit, cancellationToken); }
        catch (TimeoutException)
        { workerCancellation.Cancel(); return new(false, [], "La descarga superó el tiempo disponible. Windows está finalizando la operación."); }
        catch (OperationCanceledException) { workerCancellation.Cancel(); throw; }
        finally
        {
            _ = worker.ContinueWith(completed => { _ = completed.Exception; workerCancellation.Dispose(); },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private static DriverDownloadResult Download(IReadOnlyList<DriverUpdateInfo> offers, CancellationToken token)
    {
        object? session = null, searcher = null, searchJob = null, searchResult = null, candidates = null;
        object? chosen = null, downloader = null, downloadJob = null, downloadResult = null;
        var callback = new SearchCompletedCallback();
        var downloaded = new List<string>();
        var identities = new List<string>();
        try
        {
            token.ThrowIfCancellationRequested();
            session = Create("Microsoft.Update.Session");
            ((dynamic)session).ClientApplicationID = "Silen";
            searcher = ((dynamic)session).CreateUpdateSearcher();
            ((dynamic)searcher).Online = true;
            searchJob = ((dynamic)searcher).BeginSearch("Type='Driver' AND IsInstalled=0 AND IsHidden=0", callback, null);
            Wait(searchJob, token);
            searchResult = ((dynamic)searcher).EndSearch(searchJob);
            if ((int)((dynamic)searchResult).ResultCode != 2) return new(false, [], "La búsqueda no pudo confirmar las actualizaciones. Vuelve a buscar.");
            candidates = ((dynamic)searchResult).Updates;
            chosen = Create("Microsoft.Update.UpdateColl");
            int count = ((dynamic)candidates).Count;
            if (count > 1000) return new(false, [], "Windows devolvió demasiadas ofertas. Revisa Windows Update.");
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < count; index++)
            {
                token.ThrowIfCancellationRequested();
                object? update = null, identity = null;
                try
                {
                    update = ((dynamic)candidates).Item(index);
                    identity = ((dynamic)update).Identity;
                    string id = ((dynamic)identity).UpdateID;
                    int revision = ((dynamic)identity).RevisionNumber;
                    var offer = offers.FirstOrDefault(item => item.UpdateId.Equals(id, StringComparison.OrdinalIgnoreCase) && item.Revision == revision);
                    if (offer is null) continue;
                    found.Add(id);
                    if (!(bool)((dynamic)update).EulaAccepted) return new(false, [], "Una actualización requiere aceptar su licencia en Windows Update. No se inició la descarga.");
                    if ((bool)((dynamic)update).IsDownloaded) downloaded.Add(id);
                    else { ((dynamic)chosen).Add(update); identities.Add(id); }
                }
                finally { Release(identity); Release(update); }
            }
            if (found.Count != offers.Count) return new(false, [], "Las ofertas cambiaron. Busca actualizaciones de nuevo antes de descargar.");
            if (identities.Count == 0) return new(true, downloaded, "Las actualizaciones ya están descargadas en Windows Update.");
            token.ThrowIfCancellationRequested();
            downloader = ((dynamic)session).CreateUpdateDownloader();
            ((dynamic)downloader).Updates = chosen;
            downloadJob = ((dynamic)downloader).BeginDownload(callback, callback, null);
            Wait(downloadJob, token);
            downloadResult = ((dynamic)downloader).EndDownload(downloadJob);
            for (var index = 0; index < identities.Count; index++)
            {
                object? outcome = null;
                try { outcome = ((dynamic)downloadResult).GetUpdateResult(index); if ((int)((dynamic)outcome).ResultCode == 2) downloaded.Add(identities[index]); }
                finally { Release(outcome); }
            }
            var complete = (int)((dynamic)downloadResult).ResultCode == 2 && downloaded.Count == offers.Count;
            return new(complete, downloaded, complete ? "Descarga lista. Los drivers están en Windows Update; todavía no se han instalado." : "La descarga quedó incompleta. Puedes volver a intentarlo; no se instaló ningún driver.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { return new(false, downloaded, ex.HResult == unchecked((int)0x80070005) ? "Windows denegó la descarga. Abre Silen con permisos de administrador y revisa las políticas de Windows Update." : $"Windows no pudo completar la descarga (0x{ex.HResult:X8}). Revisa la conexión y vuelve a intentarlo."); }
        finally
        {
            Cleanup(downloadJob); Cleanup(searchJob);
            foreach (var value in new[] { downloadResult, downloadJob, downloader, chosen, candidates, searchResult, searchJob, searcher, session }) Release(value);
            GC.KeepAlive(callback);
        }
    }

    private static object Create(string id) => Activator.CreateInstance(Type.GetTypeFromProgID(id) ?? throw new COMException("Windows Update no está disponible.")) ?? throw new COMException("No se pudo iniciar Windows Update.");
    private static void Wait(object job, CancellationToken token)
    {
        while (!(bool)((dynamic)job).IsCompleted) if (token.WaitHandle.WaitOne(200)) token.ThrowIfCancellationRequested();
        token.ThrowIfCancellationRequested();
    }
    private static void Cleanup(object? job)
    {
        if (job is null) return;
        try { if (!(bool)((dynamic)job).IsCompleted) ((dynamic)job).RequestAbort(); }
        catch (COMException) { }
        try { ((dynamic)job).CleanUp(); }
        catch (COMException) { }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}
