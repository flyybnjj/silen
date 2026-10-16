using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Silen.Windows;

public sealed record DnsResolver(string Name, string Address);
public sealed record DnsBenchmarkRow(string Name, string Address, int Completed, int Failed, double? MedianMs, double? LastMs, DateTimeOffset MeasuredAt)
{
    public bool Reliable => Completed == 3 && Failed == 0;
}
public interface IDnsProbe
{
    Task<double?> QueryAsync(IPAddress server, string domain, CancellationToken token);
}
public sealed class DnsBenchmarkService(IDnsProbe? probe = null)
{
    private readonly IDnsProbe query = probe ?? new UdpDnsProbe();
    private static readonly string[] Domains = ["www.microsoft.com", "www.cloudflare.com", "www.wikipedia.org"];
    public static IReadOnlyList<DnsResolver> GetResolvers()
    {
        var resolvers = new List<DnsResolver>();
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(adapter => adapter.OperationalStatus == OperationalStatus.Up && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            foreach (var address in adapter.GetIPProperties().DnsAddresses)
                if (!address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any) && !resolvers.Any(item => item.Address == address.ToString()) && resolvers.Count < 4)
                    resolvers.Add(new("Actual · " + adapter.Name, address.ToString()));
        }
        catch (NetworkInformationException) { }
        foreach (var item in new[] { new DnsResolver("Cloudflare", "1.1.1.1"), new("Cloudflare alternativo", "1.0.0.1"), new("Google", "8.8.8.8"), new("Google alternativo", "8.8.4.4"), new("Quad9 (filtra amenazas)", "9.9.9.9"), new("Quad9 alternativo", "149.112.112.112") })
            if (!resolvers.Any(current => current.Address == item.Address)) resolvers.Add(item);
        return resolvers;
    }
    public async Task<IReadOnlyList<DnsBenchmarkRow>> MeasureAsync(IReadOnlyList<DnsResolver> resolvers, IProgress<DnsBenchmarkRow>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (resolvers.Count is < 1 or > 16) throw new ArgumentException("Elige entre uno y dieciséis servidores DNS.");
        using var concurrency = new SemaphoreSlim(3);
        var tasks = resolvers.Select(async resolver =>
        {
            if (!IPAddress.TryParse(resolver.Address, out var address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) throw new ArgumentException("Dirección DNS no válida.");
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                var times = new List<double>(); var failed = 0; DnsBenchmarkRow? row = null;
                for (var index = 0; index < Domains.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var elapsed = await query.QueryAsync(address, Domains[index], cancellationToken);
                    if (elapsed is >= 0 && double.IsFinite(elapsed.Value)) times.Add(elapsed.Value); else failed++;
                    var sorted = times.Order().ToArray();
                    double? median = sorted.Length == 0 ? null : sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
                    row = new(resolver.Name, resolver.Address, index + 1, failed, median, elapsed, DateTimeOffset.Now);
                    progress?.Report(row);
                }
                return row!;
            }
            finally { concurrency.Release(); }
        }).ToArray();
        var results = await Task.WhenAll(tasks);
        return results.OrderByDescending(row => row.Reliable).ThenBy(row => row.Failed).ThenBy(row => row.MedianMs ?? double.MaxValue).ToArray();
    }
}
