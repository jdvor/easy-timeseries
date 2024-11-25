namespace Easy.TimeSeries;

using static Constants;

internal ref struct Int64Reader
{
    private BitReader bitReader;
    private long prevValue;
    private Block prevBlock;
    private bool hasReadFirstValue;

    public readonly ColumnHeader ColumnHeader => bitReader.ColumnHeader;

    public Int64Reader(ReadOnlySpan<byte> buffer)
    {
        bitReader = new BitReader(buffer);
        prevBlock = new Block(Size64.MaxBits, Size64.MaxBits, 0);
    }

    public long Read()
    {
        if (!hasReadFirstValue)
        {
            var firstValue = (long)bitReader.Read(Size64.MaxBits);
            prevValue = firstValue;
            hasReadFirstValue = true;
            return firstValue;
        }

        var nonZeroValue = bitReader.Read(1);
        if (nonZeroValue == 0)
        {
            return prevValue;
        }

        var usePrevBlock = bitReader.Read(1);
        if (usePrevBlock == 1)
        {
            var leadingZeros = (int)bitReader.Read(Size64.LeadingZerosLengthBits);
            var blockSize = (byte)bitReader.Read(Size64.BlockSizeLengthBits) + BlockSizeAdjustment;
            var trailingZeros = Size64.MaxBits - blockSize - leadingZeros;
            prevBlock = new Block(Size64.MaxBits, leadingZeros, trailingZeros);
        }

        var xorValue = (long)bitReader.Read(prevBlock.BlockSize);
        xorValue <<= prevBlock.TrailingZeros;
        var value = xorValue ^ prevValue;
        prevValue = value;

        return value;
    }
}
