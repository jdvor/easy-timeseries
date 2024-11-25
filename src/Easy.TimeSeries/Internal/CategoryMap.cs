namespace Easy.TimeSeries;

using System.Buffers;
using System.Buffers.Binary;
using System.Text;

internal sealed class CategoryMap
{
    const int I16 = sizeof(short);
    private readonly Dictionary<string, short> labelToId;
    private readonly Dictionary<short, string> idToLabel;

    public static readonly CategoryMap Empty = new();

    public bool IsEmpty => labelToId.Count == 0;

    public int SizeHint => sizeof(ushort) + sizeof(short) + labelToId.Keys.Sum(x => x.Length + 1);

    public int Count => labelToId.Count;

    public CategoryMap(Dictionary<string, short> map)
    {
        EnsureValidMap(map);
        labelToId = map;
        idToLabel = map.ToDictionary(x => x.Value, x => x.Key);
    }

    public CategoryMap(string[] keys)
        : this(KeysToMap(keys))
    {
    }

    private static Dictionary<string, short> KeysToMap(string[] keys)
    {
        var ids = Enumerable.Range(0, keys.Length);
        return keys.Zip(ids).ToDictionary(x => x.First, x => (short)x.Second);
    }

    private CategoryMap()
    {
        labelToId = new Dictionary<string, short>(0);
        idToLabel = new Dictionary<short, string>(0);
    }

    public short GetId(string label)
    {
        if (labelToId.TryGetValue(label, out var id))
        {
            return id;
        }

        throw new InvalidOperationException("Failed to translate category label to its ID.");
    }

    public string GetLabel(short id)
    {
        if (idToLabel.TryGetValue(id, out var label))
        {
            return label;
        }

        throw new InvalidOperationException("Failed to translate category ID to its label.");
    }

    public int WriteTo(Span<byte> buffer)
    {
        var lengthSpan = buffer[..I16];
        var written = I16;
        BinaryPrimitives.WriteInt16LittleEndian(buffer[written..], (short)idToLabel.Count);
        written += I16;
        var labels = idToLabel.OrderBy(x => x.Key).Select(x => x.Value);
        foreach (var label in labels)
        {
            buffer[written] = (byte)label.Length;
            written++;
            written += Encoding.ASCII.GetBytes(label, buffer[written..]);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(lengthSpan, (ushort)(written - I16));

        return written;
    }

    public int WriteTo(IBufferWriter<byte> bufferProvider)
    {
        var span = bufferProvider.GetSpan(SizeHint);
        var written = WriteTo(span);
        bufferProvider.Advance(written);
        return written;
    }

    public static bool TryReadFrom(ReadOnlySpan<byte> buffer, out CategoryMap map, out int bytesRead)
    {
        var totalSize = (int)BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        var position = I16;
        if (totalSize > buffer.Length - position)
        {
            map = Empty;
            bytesRead = position;
            return false;
        }

        try
        {
            var mapCount = BinaryPrimitives.ReadInt16LittleEndian(buffer[position..]);
            position += I16;
            if (mapCount <= 0)
            {
                map = Empty;
                bytesRead = position;
                return false;
            }

            var labelToId = new Dictionary<string, short>(mapCount);
            for (short i = 0; i < mapCount; i++)
            {
                var labelLength = buffer[position];
                position++;
                if (labelLength == 0)
                {
                    labelToId.Add(string.Empty, i);
                    continue;
                }

                var labelSpan = buffer.Slice(position, labelLength);
                var label = Encoding.ASCII.GetString(labelSpan);
                position += labelLength;
                labelToId.Add(label, i);
            }

            map = new CategoryMap(labelToId);
            bytesRead = position;
            return true;
        }
        catch
        {
            map = Empty;
            bytesRead = position;
            return false;
        }
    }

    public static int GetTotalLength(ReadOnlySpan<byte> buffer)
        => sizeof(ushort) + BinaryPrimitives.ReadUInt16LittleEndian(buffer);

    private static void EnsureValidMap(Dictionary<string, short> map)
    {
        if (map.Count == 0)
        {
            throw new ArgumentException("The map must not be empty.", nameof(map));
        }

        if (map.Count > short.MaxValue)
        {
            throw new ArgumentException("The map is too large.", nameof(map));
        }

        var vals = map.Values.OrderBy(x => x).ToArray();
        for (var i = 0; i < map.Count; i++)
        {
            if (vals[i] != i)
            {
                throw new ArgumentException(
                    "Values in the map are not continuous sequence starting at 0.",
                    nameof(map));
            }
        }

        if (map.Keys.Any(x => x.Length > 255))
        {
            throw new ArgumentException("Keys in the map must have maximal length 255.", nameof(map));
        }
    }
}
