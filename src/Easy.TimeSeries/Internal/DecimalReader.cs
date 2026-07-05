namespace Easy.TimeSeries;

internal ref struct DecimalReader(ReadOnlySpan<byte> buffer)
{
    private BitReader bitReader = new(buffer);
    private Xor64Reader mantissaXor;
    private Xor64Reader metaXor;

    public readonly ColumnHeader ColumnHeader => bitReader.ColumnHeader;

    public decimal Read()
    {
        var mantissaLo = mantissaXor.Read(ref bitReader);
        var meta = metaXor.Read(ref bitReader);

        var lo = unchecked((int)mantissaLo);
        var mid = unchecked((int)(mantissaLo >>> 32));
        var hi = unchecked((int)meta);
        var scale = (byte)((meta >> 32) & 0xFF);
        var isNegative = (meta & (1L << 40)) != 0;

        return new decimal(lo, mid, hi, isNegative, scale);
    }
}
