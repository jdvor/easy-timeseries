namespace Easy.TimeSeries;

internal ref struct ScaledNumber64Reader(ReadOnlySpan<byte> buffer, int scale)
{
    private Int64Reader i64Reader = new(buffer);

    public readonly ColumnHeader ColumnHeader => i64Reader.ColumnHeader;

    public double Read()
    {
        var v = i64Reader.Read();
        return (double)v / scale;
    }
}
