namespace Easy.TimeSeries;

using System.Numerics;
using static Constants;

internal readonly struct Block
{
    public int LeadingZeros { get; }

    public int TrailingZeros { get; }

    public int BlockSize { get; }

    public Block(int maxBits, int leadingZeros, int trailingZeros)
    {
        Expect.Range(maxBits, 1, WordBitSize);
        Expect.Range(leadingZeros, 0, maxBits);
        Expect.Range(trailingZeros, 0, maxBits);

        LeadingZeros = leadingZeros;
        TrailingZeros = trailingZeros;
        BlockSize = maxBits - LeadingZeros - TrailingZeros;
    }

    public static Block CreateBlock64(long i64)
    {
        if (i64 < 0)
        {
            return new Block(Size64.MaxBits, 0, 0);
        }

        var u64 = (ulong)i64;
        var trailingZeros = BitOperations.TrailingZeroCount(u64); // 64 when u64 == 0
        var leadingZeros = BitOperations.LeadingZeroCount(u64);   // 64 when u64 == 0
        if (leadingZeros > Size64.MaxLeadingZerosLength)
        {
            leadingZeros = Size64.MaxLeadingZerosLength;
        }

        return new Block(Size64.MaxBits, leadingZeros, trailingZeros);
    }

    public static Block CreateBlock32(int i32)
    {
        if (i32 < 0)
        {
            return new Block(Size32.MaxBits, 0, 0);
        }

        var u32 = (uint)i32;
        var trailingZeros = BitOperations.TrailingZeroCount(u32); // 32 when u32 == 0
        var leadingZeros = BitOperations.LeadingZeroCount(u32);   // 32 when u32 == 0
        if (leadingZeros > Size32.MaxLeadingZerosLength)
        {
            leadingZeros = Size32.MaxLeadingZerosLength;
        }

        return new Block(Size32.MaxBits, leadingZeros, trailingZeros);
    }
}
