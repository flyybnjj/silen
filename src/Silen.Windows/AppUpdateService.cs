using System.Net.Http;
using System.Security.Cryptography;

namespace Silen.Windows;

public sealed class AppUpdateService : IAppUpdateService
{
    private static readonly HttpClient PublicClient = new(new HttpClientHandler { AllowAutoRedirect = false, UseDefaultCredentials = false }) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly HttpClient client;
    private readonly string cacheRoot;
    private readonly string publicKey;
    private readonly SemaphoreSlim downloadGate = new(1, 1);
    public AppUpdateService() : this(PublicClient, AppUpdatePaths.CacheRoot) { }
    internal AppUpdateService(HttpClient client, string cacheRoot, string? publicKey = null)
    { this.client = client; this.cacheRoot = cacheRoot; this.publicKey = publicKey ?? AppUpdateSignature.EmbeddedPublicKey(); }

    public async Task<AppRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        var release = await ReadReleaseAsync("latest", cancellationToken).ConfigureAwait(false);
        AppUpdateCache.Prune(cacheRoot, AppUpdatePaths.Archive(cacheRoot, release));
        return release.Version > AppUpdateTrust.Normalize(currentVersion) ? release : null;
    }
    internal async Task<AppRelease> ReadTagAsync(string tag, CancellationToken token)
    {
        AppUpdateTrust.ParseVersion(tag);
        var release = await ReadReleaseAsync("tags/" + Uri.EscapeDataString(tag), token).ConfigureAwait(false);
        if (release.Tag != tag) throw new InvalidDataException("GitHub devolvió una versión distinta de la solicitada.");
        return release;
    }
    private async Task<AppRelease> ReadReleaseAsync(string suffix, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await SendAsync(new Uri(AppUpdateTrust.ApiRoot + suffix), false, deadline.Token).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength > 1024 * 1024) throw new InvalidDataException("La respuesta de GitHub es demasiado grande.");
        await using var source = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var block = new byte[16384];
        int count;
        while ((count = await source.ReadAsync(block, deadline.Token).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + count > 1024 * 1024) throw new InvalidDataException("La respuesta de GitHub es demasiado grande.");
            buffer.Write(block, 0, count);
        }
        var metadata = buffer.ToArray();
        var release = AppUpdateTrust.Parse(metadata);
        var manifestUrl = AppUpdateSignature.ManifestUrl(metadata, release);
        using var signed = await SendAsync(new Uri(manifestUrl), true, deadline.Token).ConfigureAwait(false);
        if (signed.Content.Headers.ContentLength > AppUpdateSignature.MaxManifestBytes) throw new InvalidDataException("El manifiesto firmado supera el límite permitido.");
        await using var manifestStream = await signed.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var manifest = new MemoryStream();
        while ((count = await manifestStream.ReadAsync(block, deadline.Token).ConfigureAwait(false)) > 0)
        {
            if (manifest.Length + count > AppUpdateSignature.MaxManifestBytes) throw new InvalidDataException("El manifiesto firmado supera el límite permitido.");
            manifest.Write(block, 0, count);
        }
        AppUpdateSignature.Verify(manifest.ToArray(), release, publicKey);
        return release;
    }
    public async Task<PreparedAppUpdate> DownloadAsync(AppRelease release, IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AppUpdateTrust.Validate(release);
        await downloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? partial = null;
        try
        {
            var trusted = await ReadTagAsync(release.Tag, cancellationToken).ConfigureAwait(false);
            if (trusted != release) throw new InvalidDataException("La release cambió; busca actualizaciones nuevamente.");
            Directory.CreateDirectory(AppUpdatePaths.Validate(cacheRoot));
            var archive = AppUpdatePaths.Within(cacheRoot, AppUpdatePaths.Archive(cacheRoot, release));
            AppUpdateCache.Prune(cacheRoot, archive);
            if (File.Exists(archive) && await VerifyArchiveAsync(archive, release, cancellationToken).ConfigureAwait(false))
            { progress?.Report(new(release.Size, release.Size)); return new(release, archive); }
            partial = AppUpdatePaths.Within(cacheRoot, Path.Combine(cacheRoot, Guid.NewGuid().ToString("N") + ".partial"));
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(15));
            using var response = await SendAsync(new Uri(release.AssetUrl), true, deadline.Token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is { } length && length != release.Size) throw new InvalidDataException("El tamaño de descarga no coincide.");
            await using (var source = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false))
            await using (var destination = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var block = new byte[65536];
                long received = 0;
                int count;
                while ((count = await source.ReadAsync(block, deadline.Token).ConfigureAwait(false)) > 0)
                {
                    received += count;
                    if (received > release.Size) throw new InvalidDataException("La descarga excedió su tamaño permitido.");
                    hash.AppendData(block, 0, count);
                    await destination.WriteAsync(block.AsMemory(0, count), deadline.Token).ConfigureAwait(false);
                    progress?.Report(new(received, release.Size));
                }
                if (received != release.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("La descarga no coincide con su tamaño o SHA-256.");
                await destination.FlushAsync(deadline.Token).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partial, archive, true);
            partial = null;
            return new(release, archive);
        }
        finally
        {
            try
            {
                if (partial is not null && File.Exists(partial))
                {
                    try { File.Delete(partial); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            finally { downloadGate.Release(); }
        }
    }
    internal static async Task<bool> VerifyArchiveAsync(string path, AppRelease release, CancellationToken token)
    {
        AppUpdatePaths.Validate(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        return file.Length == release.Size && Convert.ToHexString(await SHA256.HashDataAsync(file, token).ConfigureAwait(false)).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase);
    }
    private async Task<HttpResponseMessage> SendAsync(Uri uri, bool asset, CancellationToken token)
    {
        for (var redirect = 0; redirect < 6; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Silen-Updater/1.1");
            request.Headers.Accept.ParseAdd(asset ? "application/octet-stream" : "application/vnd.github+json");
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (!asset || location is null) throw new InvalidDataException("Redirección inesperada de GitHub.");
                uri = new Uri(uri, location);
                if (uri.Scheme != "https" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) ||
                    uri.Host is not ("release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                    throw new InvalidDataException("La descarga redirige fuera del almacenamiento oficial de GitHub.");
                continue;
            }
            if (!response.IsSuccessStatusCode) { var code = response.StatusCode; response.Dispose(); throw new HttpRequestException($"GitHub no pudo completar la consulta ({(int)code}).", null, code); }
            return response;
        }
        throw new InvalidDataException("GitHub devolvió demasiadas redirecciones.");
    }
}
