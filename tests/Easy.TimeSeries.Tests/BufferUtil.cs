namespace Easy.TimeSeries.Tests;

internal static class BufferUtil
{
    public static (PooledArrayBufferWriter, BitWriter) CreateBitWriter(
        int sizeHint,
        float bufferGrowFactor = Constants.DefaultBufferGrowFactor,
        int maxAllowedBufferSize = 100_000,
        bool useColumnHeader = true)
    {
        var bufferProvider = new PooledArrayBufferWriter(sizeHint, bufferGrowFactor, maxAllowedBufferSize);
        BitWriter bitWriter;
        if (useColumnHeader)
        {
            bufferProvider.Advance(ColumnHeader.Size);
            bitWriter = new BitWriter(bufferProvider, bufferProvider.GetColumnHeaderMemory);
            return (bufferProvider, bitWriter);
        }

        bitWriter = new BitWriter(bufferProvider);
        return (bufferProvider, bitWriter);
    }
}
