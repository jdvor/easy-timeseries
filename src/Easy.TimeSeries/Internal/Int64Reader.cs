namespace Easy.TimeSeries;

internal ref struct Int64Reader(ReadOnlySpan<byte> buffer)
{
    private BitReader bitReader = new(buffer);
    private Xor64Reader xor;

    public readonly ColumnHeader ColumnHeader => bitReader.ColumnHeader;

    public long Read() => xor.Read(ref bitReader);
}
