using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Silen.Windows;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class AppUpdateServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SilenUpdateTests", Guid.NewGuid().ToString("N"));
    private static readonly byte[] Package = Encoding.UTF8.GetBytes("verified package fixture");
    internal static readonly string Hash = Convert.ToHexString(SHA256.HashData(Package));
    internal static string Metadata(string tag = "v1.2.0", string? digest = null, string? url = null, bool prerelease = false, bool duplicate = false, long? size = null, bool includeSignature = true) => JsonSerializer.Serialize(new
    {
        tag_name = tag, draft = false, prerelease,
        assets = Enumerable.Repeat(new { name = "Silen-win-x64.zip", size = size ?? Package.Length,
            digest = digest ?? "sha256:" + Hash,
            browser_download_url = url ?? $"https://github.com/flyybnjj/flyyoptimizer/releases/download/{tag}/Silen-win-x64.zip" }, duplicate ? 2 : 1)
            .Concat(includeSignature ? [new { name = "Silen-release.json", size = 1024L, digest = "", browser_download_url = $"https://github.com/flyybnjj/flyyoptimizer/releases/download/{tag}/Silen-release.json" }] : [])
    });

    private AppUpdateService Service(string metadata, byte[]? data = null) => new(new HttpClient(new Handler(request =>
        request.RequestUri!.Host == "api.github.com"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(metadata) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data ?? Package) }, metadata)), root, TestSigningKey.PublicKey);

    [Fact]
    public async Task Unsigned_release_is_rejected_even_with_valid_github_digest()
    {
        var service = Service(Metadata(includeSignature: false));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.CheckAsync(new Version(1, 1, 0)));
    }

    [Fact]
    public async Task Stable_newer_release_is_downloaded_verified_and_reused()
    {
        var service = Service(Metadata());
        var release = Assert.IsType<AppRelease>(await service.CheckAsync(new Version(1, 1, 0)));
        var prepared = await service.DownloadAsync(release);
        Assert.Equal(Package, await File.ReadAllBytesAsync(prepared.ArchivePath));
        Assert.Equal(prepared.ArchivePath, (await service.DownloadAsync(release)).ArchivePath);
        Assert.Empty(Directory.GetFiles(root, "*.partial", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("v1.1.0")]
    [InlineData("v1.0.0")]
    public async Task Equal_and_older_versions_do_not_offer_update(string tag) => Assert.Null(await Service(Metadata(tag)).CheckAsync(new Version(1, 1, 0)));

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("prerelease")]
    [InlineData("suffix")]
    [InlineData("foreign")]
    [InlineData("http")]
    [InlineData("oversize")]
    public async Task Untrusted_metadata_is_rejected(string kind)
    {
        var json = kind switch
        {
            "missing" => Metadata(digest: ""), "duplicate" => Metadata(duplicate: true),
            "prerelease" => Metadata(prerelease: true), "suffix" => Metadata(tag: "v1.2.0-beta"),
            "foreign" => Metadata(url: "https://evil.example/Silen-win-x64.zip"),
            "http" => Metadata(url: "http://github.com/flyybnjj/flyyoptimizer/releases/download/v1.2.0/Silen-win-x64.zip"),
            _ => Metadata(size: long.MaxValue)
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => Service(json).CheckAsync(new Version(1, 1, 0)));
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("short")]
    [InlineData("long")]
    public async Task Damaged_download_is_never_published(string kind)
    {
        var bytes = kind switch { "short" => Package[..^1], "long" => Package.Concat(new byte[1]).ToArray(), _ => new byte[Package.Length] };
        var service = Service(Metadata(), bytes);
        var release = (await service.CheckAsync(new Version(1, 1, 0)))!;
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(release));
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Cancelled_download_leaves_no_ready_or_partial_file()
    {
        var service = Service(Metadata());
        var release = (await service.CheckAsync(new Version(1, 1, 0)))!;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DownloadAsync(release, cancellationToken: cancellation.Token));
        Assert.False(Directory.Exists(root) && Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task Forged_release_is_revalidated_before_asset_download()
    {
        var service = Service(Metadata());
        var release = (await service.CheckAsync(new Version(1, 1, 0)))!;
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(release with { Sha256 = new string('0', 64) }));
    }

    [Theory]
    [InlineData("tag")]
    [InlineData("digest")]
    public async Task Trailing_newline_in_release_identity_is_rejected(string field)
    {
        var json = field == "tag" ? Metadata(tag: "v1.2.0\n") : Metadata(digest: "sha256:" + Hash + "\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => Service(json).CheckAsync(new Version(1, 1, 0)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_during_streaming_preserves_original_error_and_allows_retry_even_when_cleanup_fails(bool blockCleanup)
    {
        var package = new byte[150000];
        Random.Shared.NextBytes(package);
        var json = Metadata(size: package.Length, digest: "sha256:" + Convert.ToHexString(SHA256.HashData(package)));
        var service = new AppUpdateService(new HttpClient(new Handler(request => new(HttpStatusCode.OK)
        { Content = request.RequestUri!.Host == "api.github.com" ? new StringContent(json) : new StreamContent(new NonSeekStream(package)) }, json)), root, TestSigningKey.PublicKey);
        var release = (await service.CheckAsync(new Version(1, 1, 0)))!;
        using var cancellation = new CancellationTokenSource();
        var progress = new ImmediateProgress(_ =>
        {
            if (blockCleanup)
                foreach (var partial in Directory.GetFiles(root, "*.partial")) File.SetAttributes(partial, FileAttributes.ReadOnly);
            cancellation.Cancel();
        });
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DownloadAsync(release, progress, cancellation.Token));
            Assert.Empty(Directory.GetFiles(root, "*.zip"));
            var completed = await service.DownloadAsync(release).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(package, await File.ReadAllBytesAsync(completed.ArchivePath));
        }
        finally
        {
            foreach (var path in Directory.GetFiles(root)) File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Chunked_size_mismatch_is_bounded_without_content_length(int difference)
    {
        var service = new AppUpdateService(new HttpClient(new Handler(request => new(HttpStatusCode.OK)
        { Content = request.RequestUri!.Host == "api.github.com" ? new StringContent(Metadata()) : new StreamContent(new NonSeekStream(new byte[Package.Length + difference])) })), root, TestSigningKey.PublicKey);
        var release = (await service.CheckAsync(new Version(1, 1, 0)))!;
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(release));
        Assert.Empty(Directory.GetFiles(root));
    }

    [Fact]
    public async Task Chunked_metadata_over_one_megabyte_is_rejected()
    {
        var service = new AppUpdateService(new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new NonSeekStream(new byte[1024 * 1024 + 1])) })), root);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.CheckAsync(new Version(1, 1, 0)));
    }

    [Theory]
    [InlineData("https://evil.example/package")]
    [InlineData("http://release-assets.githubusercontent.com/package")]
    [InlineData("https://release-assets.githubusercontent.com:444/package")]
    [InlineData("https://user@release-assets.githubusercontent.com/package")]
    public async Task Asset_redirect_to_untrusted_source_is_rejected(string destination)
    {
        var service = new AppUpdateService(new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.Host == "api.github.com") return new(HttpStatusCode.OK) { Content = new StringContent(Metadata()) };
            var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
            redirect.Headers.Location = new Uri(destination);
            return redirect;
        })), root, TestSigningKey.PublicKey);
        var release = (await service.CheckAsync(new Version(1, 1, 0)))!;
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(release));
    }

    [Fact]
    public async Task Corrupt_cache_is_replaced_only_by_verified_download()
    {
        var service = Service(Metadata());
        var release = (await service.CheckAsync(new Version(1, 1, 0)))!;
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, Hash + ".zip");
        File.WriteAllBytes(path, new byte[Package.Length]);
        Assert.Equal(Package, await File.ReadAllBytesAsync((await service.DownloadAsync(release)).ArchivePath));
    }
    [Fact]
    public async Task Unsupported_cache_does_not_prevent_service_construction_or_metadata_check()
    {
        var service = new AppUpdateService(new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(Metadata()) })), "unsupported-relative-cache", TestSigningKey.PublicKey);
        var release = Assert.IsType<AppRelease>(await service.CheckAsync(new Version(1, 1, 0)));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(release));
        Assert.False(Directory.Exists("unsupported-relative-cache"));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond, string? metadata = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/Silen-release.json", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(TestSigningKey.Manifest(AppUpdateTrust.Parse(Encoding.UTF8.GetBytes(metadata ?? Metadata())))) }
                : respond(request));
    }
    private sealed class ImmediateProgress(Action<AppUpdateProgress> report) : IProgress<AppUpdateProgress>
    {
        public void Report(AppUpdateProgress value) => report(value);
    }
    private sealed class NonSeekStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream inner = new(bytes);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}

