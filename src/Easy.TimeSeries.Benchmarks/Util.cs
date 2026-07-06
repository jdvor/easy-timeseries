namespace Easy.TimeSeries.Benchmarks;

internal static class Util
{
    internal static PooledArrayBufferWriter CreateFramedBuffer(int sizeHint)
    {
        var size = sizeHint + ColumnHeader.Size;
        var buffer = new PooledArrayBufferWriter(size, Constants.DefaultBufferGrowFactor, Constants.MaxAllowedBufferSize);
        buffer.Advance(ColumnHeader.Size);
        return buffer;
    }
}
