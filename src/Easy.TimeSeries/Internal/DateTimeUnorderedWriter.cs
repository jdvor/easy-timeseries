namespace Easy.TimeSeries;

using static Constants.TimeStamp;

/// <summary>
/// Absolute-timestamp writer for <see cref="DateTime"/> columns whose values are not sorted.
/// The epoch-relative scaled timestamp is routed through <see cref="Xor64Writer"/>, so values
/// may appear in any order, repeat, or precede the epoch. Use <see cref="DateTimeOrderedWriter"/>
/// (delta-of-delta) when the column is monotonically non-decreasing - it compresses better.
/// </summary>
internal sealed class DateTimeUnorderedWriter
{
    private readonly BitWriter bitWriter;
    private readonly long precisionDivisor;
    private Xor64Writer xor;

    public DateTimeUnorderedWriter(BitWriter bitWriter, TimePrecision precision)
    {
        this.bitWriter = bitWriter;
        precisionDivisor = Util.GetPrecisionDivisor(precision);
    }

    public void Write(DateTime value)
    {
        Expect.Utc(value);

        var timestamp = (value.Ticks - Epoch.Ticks) / precisionDivisor;
        xor.Write(bitWriter, timestamp);
        bitWriter.CommitRecord();
    }

    public static int GetSizeHint(int valueCount, TimePrecision precision)
    {
        return precision switch
        {
            TimePrecision.Seconds => valueCount * 3,
            TimePrecision.TenthsOfSecond => valueCount * 4,
            _ => valueCount * 5,
        };
    }
}
