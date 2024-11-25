namespace Easy.TimeSeries;

using System.Runtime.CompilerServices;
using static Constants.TimeStamp;

internal ref struct DateTimeReader
{
    private readonly long precision;
    private BitReader bitReader;
    private long prevTimeStamp;
    private long prevTimeStampDelta;
    private bool hasReadFirstValue;

    public readonly ColumnHeader ColumnHeader => bitReader.ColumnHeader;

    public DateTimeReader(ReadOnlySpan<byte> buffer, TimePrecision precision)
    {
        bitReader = new BitReader(buffer);
        this.precision = Util.GetPrecisionDivisor(precision);
    }

    public DateTime Read()
    {
        if (!hasReadFirstValue)
        {
            prevTimeStamp = (long)bitReader.Read(MaxBits);
            prevTimeStampDelta = 1;
            hasReadFirstValue = true;
            return GetDateTime(prevTimeStamp, precision);
        }

        if (bitReader.Read(1) == 1)
        {
            // there is delta of delta
            var prefix = bitReader.Read(2);
            var (encodedBits, maxEncodedValue) = GetLayout(prefix);
            var encodedValue = (long)bitReader.Read(encodedBits);

            // [0, 255] becomes [-128, 127]
            encodedValue -= maxEncodedValue;

            prevTimeStampDelta += encodedValue;
        }

        prevTimeStamp += prevTimeStampDelta;
        return GetDateTime(prevTimeStamp, precision);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static DateTime GetDateTime(long timeStamp, long precision)
    {
        var ticks = Epoch.Ticks + (timeStamp * precision);
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    internal static (int encodedBits, long maxEncodedValue) GetLayout(ulong prefix)
    {
        return prefix switch
        {
            DeltaPrefix1 => (DeltaBits1, DeltaMaxValue1),
            DeltaPrefix2 => (DeltaBits2, DeltaMaxValue2),
            DeltaPrefix3 => (DeltaBits3, DeltaMaxValue3),
            DeltaPrefix4 => (DeltaBits4, DeltaMaxValue4),
            _ => throw new ArgumentException("...", nameof(prefix)),
        };
    }

    public static uint GetSizeHint(int valueCount, TimePrecision precision)
    {
        return precision switch
        {
            TimePrecision.Seconds => (uint)(valueCount * 1.5),
            TimePrecision.TenthsOfSecond => (uint)valueCount * 2,
            _ => (uint)valueCount * 3,
        };
    }
}
