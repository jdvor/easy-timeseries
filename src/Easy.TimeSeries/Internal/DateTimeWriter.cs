namespace Easy.TimeSeries;

using System.Runtime.CompilerServices;
using static Constants.TimeStamp;

internal sealed class DateTimeWriter
{
    private readonly BitWriter bitWriter;
    private readonly long precisionDivisor;
    private long prevTimeStamp;
    private long prevTimeStampDelta;
    private bool hasStoredFirstValue;

    public DateTimeWriter(BitWriter bitWriter, TimePrecision precision)
    {
        this.bitWriter = bitWriter;
        precisionDivisor = Util.GetPrecisionDivisor(precision);
    }

    public void Write(DateTime value)
    {
        Expect.Utc(value);

        var timestamp = GetTimeStamp(value);
        EnsureIncreasingTime(timestamp);

        if (!hasStoredFirstValue)
        {
            bitWriter.Write(timestamp, MaxBits);
            prevTimeStamp = timestamp;
            prevTimeStampDelta = 1;
            hasStoredFirstValue = true;
            return;
        }

        var delta = timestamp - prevTimeStamp;
        var deltaOfDelta = delta - prevTimeStampDelta;
        if (deltaOfDelta == 0)
        {
            prevTimeStamp = timestamp;
            bitWriter.Write(0, 1); // signal no delta
            return;
        }

        bitWriter.Write(1, 1); // signal there is delta
        var (encoded, encodedBits, prefix, prefixBits) = Encode(deltaOfDelta);
        bitWriter.Write(prefix, prefixBits);
        bitWriter.Write(encoded, encodedBits);

        prevTimeStamp = timestamp;
        prevTimeStampDelta = delta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureIncreasingTime(long newTimestamp)
    {
        if (newTimestamp < prevTimeStamp)
        {
            throw new InvalidOperationException("The appending date is not greater than the previous appended date.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetTimeStamp(DateTime dt)
    {
        return (dt.Ticks - Epoch.Ticks) / precisionDivisor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (ulong encoded, int encodedBits, ulong prefix, int prefixBits) Encode(long deltaOfDelta)
    {
        var absDod = Math.Abs(deltaOfDelta);
        return absDod switch
        {
            < DeltaMaxValue1 => ((ulong)(deltaOfDelta + DeltaMaxValue1), DeltaBits1, DeltaPrefix1, PrefixBits),
            < DeltaMaxValue2 => ((ulong)(deltaOfDelta + DeltaMaxValue2), DeltaBits2, DeltaPrefix2, PrefixBits),
            < DeltaMaxValue3 => ((ulong)(deltaOfDelta + DeltaMaxValue3), DeltaBits3, DeltaPrefix3, PrefixBits),
            _ => ((ulong)(deltaOfDelta + DeltaMaxValue4), DeltaBits4, DeltaPrefix4, PrefixBits),
        };
    }

    public static int GetSizeHint(int valueCount, TimePrecision precision)
    {
        return precision switch
        {
            TimePrecision.Seconds => (int)(valueCount * 1.5),
            TimePrecision.TenthsOfSecond => valueCount * 2,
            _ => valueCount * 3,
        };
    }
}
