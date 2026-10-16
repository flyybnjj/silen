using System.Buffers.Binary;
using System.Text;

namespace Silen.Windows;

internal static class DnsWireProtocol
{
    internal static byte[] CreateQuery(ushort id, string domain)
    {
        if (domain.Length is < 1 or > 253 || domain.Any(character => character > 127)) throw new ArgumentException("Nombre DNS inválido.");
        using var output = new MemoryStream();
        output.Write([(byte)(id >> 8), (byte)id, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0]);
        foreach (var label in domain.Split('.'))
        {
            if (label.Length is < 1 or > 63 || label.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')) throw new ArgumentException("Etiqueta DNS inválida.");
            output.WriteByte((byte)label.Length); output.Write(Encoding.ASCII.GetBytes(label));
        }
        output.Write([0, 0, 1, 0, 1]); return output.ToArray();
    }
    internal static bool Truncated(byte[] query, byte[] response) => response.Length >= 12 && response[0] == query[0] && response[1] == query[1] && (response[2] & 0x82) == 0x82;
    internal static bool HasAnswer(byte[] query, byte[] response)
    {
        if (query.Length < 17 || response.Length < query.Length || response[0] != query[0] || response[1] != query[1] || (response[2] & 0xfa) != 0x80 || (response[3] & 15) != 0 || response[4] != 0 || response[5] != 1 ||
            !response.AsSpan(12, query.Length - 12).SequenceEqual(query.AsSpan(12))) return false;
        var answers = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(6));
        if (answers is 0 or > 128) return false;
        var questionOffset = 12;
        if (!ReadName(query, ref questionOffset, out var question)) return false;
        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var offset = query.Length;
        for (var record = 0; record < answers; record++)
        {
            if (!ReadName(response, ref offset, out var owner) || offset + 10 > response.Length) return false;
            var type = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset));
            var recordClass = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset + 2));
            var length = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset + 8)); offset += 10;
            if (offset + length > response.Length) return false;
            if (type == 1 && recordClass == 1 && length == 4) addresses.Add(owner);
            if (type == 5 && recordClass == 1)
            {
                var end = offset;
                if (!ReadName(response, ref end, out var alias) || end != offset + length) return false;
                aliases[owner] = alias;
            }
            offset += length;
        }
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (visited.Add(question))
        {
            if (addresses.Contains(question)) return true;
            if (!aliases.TryGetValue(question, out var alias)) return false;
            question = alias;
        }
        return false;
    }
    private static bool ReadName(byte[] packet, ref int offset, out string name)
    {
        name = ""; var position = offset; var jumped = false; var visited = new HashSet<int>(); var labels = new List<string>(); var size = 0;
        while (position < packet.Length && visited.Count < 128 && visited.Add(position))
        {
            var start = position; var length = packet[position++];
            if (length == 0) { if (!jumped) offset = position; name = string.Join('.', labels); return labels.Count > 0; }
            if ((length & 0xc0) == 0xc0)
            {
                if (position >= packet.Length) return false;
                var pointer = ((length & 63) << 8) | packet[position++];
                if (pointer >= start) return false; // La compresión solo puede apuntar a un nombre anterior.
                if (!jumped) offset = position;
                jumped = true; position = pointer; continue;
            }
            if (length > 63 || position + length > packet.Length || (size += length + 1) > 255) return false;
            var bytes = packet.AsSpan(position, length);
            if (bytes.Contains((byte)0) || bytes.IndexOfAnyInRange((byte)128, byte.MaxValue) >= 0) return false;
            labels.Add(Encoding.ASCII.GetString(bytes)); position += length;
        }
        return false;
    }
}
