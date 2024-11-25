namespace Easy.TimeSeries;

using static Constants;

internal ref struct Int32Reader
{
    private BitReader bitReader;
    private int prevValue;
    private Block prevBlock;
    private bool hasReadFirstValue;

    public readonly ColumnHeader ColumnHeader => bitReader.ColumnHeader;

    public Int32Reader(ReadOnlySpan<byte> buffer)
    {
        bitReader = new BitReader(buffer);
        prevBlock = new Block(Size32.MaxBits, Size32.MaxBits, 0);
    }

    public int Read()
    {
        if (!hasReadFirstValue)
        {
            var firstValue = (int)bitReader.Read(Size32.MaxBits);
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
            var leadingZeros = (int)bitReader.Read(Size32.LeadingZerosLengthBits);
            var blockSize = (byte)bitReader.Read(Size32.BlockSizeLengthBits) + BlockSizeAdjustment;
            var trailingZeros = Size32.MaxBits - blockSize - leadingZeros;
            prevBlock = new Block(Size32.MaxBits, leadingZeros, trailingZeros);
        }

        var xorValue = (int)bitReader.Read(prevBlock.BlockSize);
        xorValue <<= prevBlock.TrailingZeros;
        var value = xorValue ^ prevValue;
        prevValue = value;

        return value;
    }
}
