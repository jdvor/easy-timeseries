namespace Easy.TimeSeries;

internal ref struct DoubleReader(ReadOnlySpan<byte> buffer)
{
    public readonly ColumnHeader ColumnHeader => i64Reader.ColumnHeader;

    private Int64Reader i64Reader = new(buffer);

    public double Read()
        => BitConverter.Int64BitsToDouble(i64Reader.Read());
}
