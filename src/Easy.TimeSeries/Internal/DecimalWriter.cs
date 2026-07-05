namespace Easy.TimeSeries;

/// <summary>
/// Stores decimals exactly (full 96-bit mantissa, scale, and sign) as two interleaved
/// XOR + block streams per record: the low 64 bits of the mantissa, and a "meta" word packing
/// the high 32 mantissa bits, the scale, and the sign. For typical columns (fixed number of
/// decimal places, absolute values below ~1.8e19) the meta word is constant, so it costs a
/// single bit per record after the first one.
/// </summary>
internal sealed class DecimalWriter(BitWriter bitWriter)
{
    private Xor64Writer mantissaXor;
    private Xor64Writer metaXor;

    public void Write(decimal value)
    {
        Span<int> parts = stackalloc int[4];
        decimal.GetBits(value, parts);

        var mantissaLo = (uint)parts[0] | ((long)(uint)parts[1] << 32);

        // parts[3] flags layout: bits 16-23 = scale (0-28), bit 31 = sign.
        var scale = (parts[3] >> 16) & 0xFF;
        var sign = (parts[3] >>> 31) & 1;
        var meta = (long)(uint)parts[2] | ((long)scale << 32) | ((long)sign << 40);

        mantissaXor.Write(bitWriter, mantissaLo);
        metaXor.Write(bitWriter, meta);
        bitWriter.CommitRecord();
    }

    public static int GetSizeHint(int valueCount)
        => valueCount * 5;
}
