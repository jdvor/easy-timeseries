namespace Easy.TimeSeries;

using static Constants;

/// <summary>
/// Decoding counterpart of <see cref="Xor64Writer"/>. Holds only the XOR + block state; the
/// <see cref="BitReader"/> is passed by reference so that several streams can share one
/// bit sequence, as <see cref="DecimalReader"/> does.
/// </summary>
internal struct Xor64Reader
{
    private Block prevBlock;
    private long prevValue;
    private bool hasReadFirstValue;

    public long Read(ref BitReader bitReader)
    {
        if (!hasReadFirstValue)
        {
            var firstValue = (long)bitReader.Read(Size64.MaxBits);
            prevBlock = new Block(Size64.MaxBits, Size64.MaxBits, 0);
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
