namespace Easy.TimeSeries;

using static Constants;

internal sealed class Int32Writer
{
    private readonly BitWriter bitWriter;
    private Block prevBlock;
    private int prevValue;
    private bool hasStoredFirstValue;

    public Int32Writer(BitWriter bitWriter)
    {
        this.bitWriter = bitWriter;
        prevBlock = new Block(Size32.MaxBits, Size32.MaxBits, 0);
    }

    public void Write(int value)
    {
        if (!hasStoredFirstValue)
        {
            bitWriter.Write(value, Size32.MaxBits);
            prevValue = value;
            hasStoredFirstValue = true;
            return;
        }

        var xorWithPrev = prevValue ^ value;
        if (xorWithPrev == 0)
        {
            // It's the same value.
            bitWriter.Write(0, 1);
            return;
        }

        // There's delta from previous value.
        bitWriter.Write(1, 1);

        var currBlock = Block.CreateBlock32(xorWithPrev);
        if (currBlock.LeadingZeros >= prevBlock.LeadingZeros && currBlock.TrailingZeros >= prevBlock.TrailingZeros)
        {
            // Control bit saying we should use the previous block information.
            bitWriter.Write(0, 1);

            // Write the parts of the value that changed.
            var blockValue = xorWithPrev >> prevBlock.TrailingZeros;
            bitWriter.Write(blockValue, prevBlock.BlockSize);
        }
        else
        {
            // Control bit saying we need to provide new block information.
            bitWriter.Write(1, 1);

            // Details about the new block information
            bitWriter.Write(currBlock.LeadingZeros, Size32.LeadingZerosLengthBits);

            bitWriter.Write(currBlock.BlockSize - BlockSizeAdjustment, Size32.BlockSizeLengthBits);

            // Write the parts of the value that changed.
            var blockValue = xorWithPrev >> currBlock.TrailingZeros;
            bitWriter.Write(blockValue, currBlock.BlockSize);

            prevBlock = currBlock;
        }

        prevValue = value;
    }

    public static int GetSizeHint(int valueCount)
        => valueCount * 3;
}
