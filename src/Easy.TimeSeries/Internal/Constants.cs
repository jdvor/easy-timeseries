// ReSharper disable InconsistentNaming (false positives for TimeStamp.Delta*
namespace Easy.TimeSeries;

internal static class Constants
{
    public const int WordByteSize = sizeof(ulong);
    public const int WordBitSize = WordByteSize * 8;
    public const int DefaultInitialBufferSize = 20 * 1024;
    public const float DefaultBufferGrowFactor = 0.7f;
    public const int MaxAllowedBufferSize = int.MaxValue; // 2GB
    public const int BlockSizeAdjustment = 1;

    public static class Size32
    {
        public const int MaxBits = 32;
        public const int BlockSizeLengthBits = 5;
        public const int LeadingZerosLengthBits = 4;
        public const int MaxLeadingZerosLength = (1 << LeadingZerosLengthBits) - 1;

        internal static readonly byte[] LeadingZerosLookup =
        [
            32, 31, 30, 30, 29, 29, 29, 29,
            28, 28, 28, 28, 28, 28, 28, 28,
            27, 27, 27, 27, 27, 27, 27, 27,
            27, 27, 27, 27, 27, 27, 27, 27,
            26, 26, 26, 26, 26, 26, 26, 26,
            26, 26, 26, 26, 26, 26, 26, 26,
            26, 26, 26, 26, 26, 26, 26, 26,
            26, 26, 26, 26, 26, 26, 26, 26,
            25, 25, 25, 25, 25, 25, 25, 25,
            25, 25, 25, 25, 25, 25, 25, 25,
            25, 25, 25, 25, 25, 25, 25, 25,
            25, 25, 25, 25, 25, 25, 25, 25,
            25, 25, 25, 25, 25, 25, 25, 25,
            25, 25, 25, 25, 25, 25, 25, 25,
            25, 25, 25, 25, 25, 25, 25, 25,
            25, 25, 25, 25, 25, 25, 25, 25,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24,
            24, 24, 24, 24, 24, 24, 24, 24
        ];
    }

    public static class Size64
    {
        public const int MaxBits = 64;
        public const int BlockSizeLengthBits = 6;
        public const int LeadingZerosLengthBits = 5;
        public const int MaxLeadingZerosLength = (1 << LeadingZerosLengthBits) - 1;
    }

    public static class TimeStamp
    {
        // 2^41 - 1 => 2_199_023_255_551 milliseconds since Epoch => max time 2069-09-06T15:47:35.551Z for ms precision
        public const int MaxBits = 41;

        public const int PrefixBits = 2;

        public const int DeltaBits1 = 3;
        public const long DeltaMaxValue1 = 1L << (DeltaBits1 - 1);
        public const ulong DeltaPrefix1 = 0b00;

        public const int DeltaBits2 = 7;
        public const long DeltaMaxValue2 = 1L << (DeltaBits2 - 1);
        public const ulong DeltaPrefix2 = 0b01;

        public const int DeltaBits3 = 12;
        public const long DeltaMaxValue3 = 1L << (DeltaBits3 - 1);
        public const ulong DeltaPrefix3 = 0b10;

        public const int DeltaBits4 = 32;
        public const long DeltaMaxValue4 = 1L << (DeltaBits4 - 1);
        public const ulong DeltaPrefix4 = 0b11;

        public static readonly DateTime Epoch = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
