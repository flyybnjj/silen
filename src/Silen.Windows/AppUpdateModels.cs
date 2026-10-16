namespace Silen.Windows;

public sealed record AppRelease(Version Version, string Tag, string AssetUrl, long Size, string Sha256);
public sealed record AppUpdateProgress(long Received, long Total);
public sealed record PreparedAppUpdate(AppRelease Release, string ArchivePath);

public interface IAppUpdateService
{
    Task<AppRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default);
    Task<PreparedAppUpdate> DownloadAsync(AppRelease release, IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default);
}
