namespace Easy.TimeSeries;

internal ref struct ScaledNumber32Reader(ReadOnlySpan<byte> buffer, int scale)
{
    private Int32Reader i32Reader = new(buffer);

    public readonly ColumnHeader ColumnHeader => i32Reader.ColumnHeader;

    public float Read()
    {
        var v = i32Reader.Read();
        return (float)v / scale;
    }
}
