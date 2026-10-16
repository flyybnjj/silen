using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Silen.Windows;

public sealed class UdpDnsProbe : IDnsProbe
{
    public async Task<double?> QueryAsync(IPAddress server, string domain, CancellationToken token)
    {
        var query = DnsWireProtocol.CreateQuery((ushort)RandomNumberGenerator.GetInt32(65536), domain);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            using var udp = new UdpClient(server.AddressFamily); udp.Connect(new IPEndPoint(server, 53));
            var watch = Stopwatch.StartNew();
            await udp.SendAsync(query, timeout.Token);
            var response = (await udp.ReceiveAsync(timeout.Token)).Buffer;
            if (DnsWireProtocol.Truncated(query, response))
            {
                using var tcp = new TcpClient(server.AddressFamily); await tcp.ConnectAsync(server, 53, timeout.Token);
                await using var stream = tcp.GetStream(); var length = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(length, checked((ushort)query.Length));
                await stream.WriteAsync(length, timeout.Token); await stream.WriteAsync(query, timeout.Token);
                await stream.ReadExactlyAsync(length, timeout.Token);
                var size = BinaryPrimitives.ReadUInt16BigEndian(length); if (size < 12) return null;
                response = new byte[size]; await stream.ReadExactlyAsync(response, timeout.Token);
            }
            return DnsWireProtocol.HasAnswer(query, response) ? watch.Elapsed.TotalMilliseconds : null;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return null; }
        catch (Exception ex) when (ex is SocketException or IOException) { return null; }
    }
}
