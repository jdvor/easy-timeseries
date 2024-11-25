namespace Easy.TimeSeries;

internal ref struct FloatReader(ReadOnlySpan<byte> buffer)
{
    public readonly ColumnHeader ColumnHeader => i32Reader.ColumnHeader;

    private Int32Reader i32Reader = new(buffer);

    public float Read()
        => BitConverter.Int32BitsToSingle(i32Reader.Read());
}
