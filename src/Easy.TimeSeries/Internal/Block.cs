namespace Easy.TimeSeries;

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
        var trailingZeros = Size64.MaxBits;
        ulong mask = 1;
        for (var i = 0; i < Size64.MaxBits; ++i, mask <<= 1)
        {
            if ((u64 & mask) == 0)
            {
                continue;
            }

            trailingZeros = i;
            break;
        }

        var leadingZeros = CountLeadingZeros64(u64);
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
        var trailingZeros = Size32.MaxBits;
        uint mask = 1;
        for (var i = 0; i < Size32.MaxBits; ++i, mask <<= 1)
        {
            if ((u32 & mask) == 0)
            {
                continue;
            }

            trailingZeros = i;
            break;
        }

        var leadingZeros = CountLeadingZeros32(u32);
        if (leadingZeros > Size32.MaxLeadingZerosLength)
        {
            leadingZeros = Size32.MaxLeadingZerosLength;
        }

        return new Block(Size32.MaxBits, leadingZeros, trailingZeros);
    }

    private static int CountLeadingZeros64(ulong x)
    {
        if (x >= 1L << 32)
        {
            // There is a non-zero in the upper 32 bits; just count that DWORD.
            return CountLeadingZeros32((uint)(x >> 32));
        }

        // The whole upper DWORD was zero so count the lower DWORD plus the 32 bits from the upper.
        return 32 + CountLeadingZeros32((uint)(x & 0xFFFF_FFFF));
    }

    private static int CountLeadingZeros32(uint x)
    {
        var n = x switch
        {
            >= 1U << 24 => 24,
            >= 1U << 16 => 16,
            >= 1U << 8 => 8,
            _ => 0,
        };

        // GetLeadingZeros(x >> n) is slower than using lookup table (~ 450ns vs 100ns),
        // but we do not need to allocate anything on heap. Maybe worth it?
        ////return GetLeadingZeros(x >> n) - n;
        return Size32.LeadingZerosLookup[x >> n] - n;
    }

    /*
    private static byte GetLeadingZeros(uint n)
    {
        return n switch
        {
            0 => 32,
            1 => 31,
            2 => 30,
            3 => 30,
            <= 7 => 29,
            <= 15 => 28,
            <= 31 => 27,
            <= 63 => 26,
            <= 127 => 25,
            <= 255 => 24,
            _ => throw new ArgumentException("this should be impossible", nameof(n)),
        };
    }
    */
}
