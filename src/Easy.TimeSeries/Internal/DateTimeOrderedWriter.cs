namespace Easy.TimeSeries;

using System.Runtime.CompilerServices;
using static Constants.TimeStamp;

/// <summary>
/// Delta-of-delta writer for <see cref="DateTime"/> columns whose values are sorted in ascending
/// order (monotonically non-decreasing); <see cref="Write"/> throws when a value is smaller than
/// its predecessor. Use <see cref="DateTimeUnorderedWriter"/> when the column is not sorted.
/// </summary>
internal sealed class DateTimeOrderedWriter
{
    private readonly BitWriter bitWriter;
    private readonly long precisionDivisor;
    private long prevTimeStamp;
    private long prevTimeStampDelta;
    private bool hasStoredFirstValue;

    public DateTimeOrderedWriter(BitWriter bitWriter, TimePrecision precision)
    {
        this.bitWriter = bitWriter;
        precisionDivisor = Util.GetPrecisionDivisor(precision);
    }

    public void Write(DateTime value)
    {
        Expect.Utc(value);

        var timestamp = GetTimeStamp(value);
        EnsureRepresentable(timestamp, value);
        EnsureIncreasingTime(timestamp);

        if (!hasStoredFirstValue)
        {
            bitWriter.Write(timestamp, MaxBits);
            prevTimeStamp = timestamp;
            prevTimeStampDelta = 1;
            hasStoredFirstValue = true;
            bitWriter.CommitRecord();
            return;
        }

        var delta = timestamp - prevTimeStamp;
        var deltaOfDelta = delta - prevTimeStampDelta;
        if (deltaOfDelta == 0)
        {
            prevTimeStamp = timestamp;
            bitWriter.Write(0, 1); // signal no delta
            bitWriter.CommitRecord();
            return;
        }

        bitWriter.Write(1, 1); // signal there is delta
        var (encoded, encodedBits, prefix, prefixBits) = Encode(deltaOfDelta);
        bitWriter.Write(prefix, prefixBits);
        bitWriter.Write(encoded, encodedBits);

        prevTimeStamp = timestamp;
        prevTimeStampDelta = delta;
        bitWriter.CommitRecord();
    }

    /// <summary>
    /// The timestamp field is <see cref="MaxBits"/> bits wide, so a value past that ceiling would be truncated by
    /// the bit writer and read back as a plausible but wrong instant. Fail loudly instead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureRepresentable(long timestamp, DateTime value)
    {
        if (timestamp is < 0 or > MaxTimeStamp)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "DateTimeOrdered columns store the timestamp in a "
                + MaxBits
                + "-bit field counted from " + "2000-01-01T00:00:00Z, so the value is out of range at this "
                + "precision. Use a coarser TimePrecision, or AddTimeUnordered (DateTimeUnordered), which stores "
                + "the full value.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureIncreasingTime(long newTimestamp)
    {
        if (newTimestamp < prevTimeStamp)
        {
            throw new InvalidOperationException(
                "DateTimeOrdered columns require values sorted in ascending order; "
                + "use AddTimeUnordered (DateTimeUnordered) for unsorted values.");
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
