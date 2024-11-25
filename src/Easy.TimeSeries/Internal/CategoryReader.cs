namespace Easy.TimeSeries;

using System.Buffers.Binary;

internal ref struct CategoryReader(ReadOnlySpan<byte> buffer, CategoryMap map)
{
    private BitReader bitReader = new(buffer);

    public readonly ColumnHeader ColumnHeader => bitReader.ColumnHeader;

    public CategoryReader(ReadOnlySpan<byte> buffer)
        : this(buffer, ReadCategoryMap(buffer))
    {
    }

    public string Read()
    {
        var isSmall = bitReader.Read(1) == 0;
        var id = isSmall
            ? (short)bitReader.Read(4)
            : (short)bitReader.Read(15);

        var label = map.GetLabel(id);

        return label;
    }

    private static CategoryMap ReadCategoryMap(ReadOnlySpan<byte> buffer)
    {
        var mapPosition = (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer) + ColumnHeader.Size;
        if (CategoryMap.TryReadFrom(buffer[mapPosition..], out var map, out _))
        {
            return map;
        }

        throw new ArgumentException("Failed to read CategoryMap from the buffer.", nameof(buffer));
    }
}
