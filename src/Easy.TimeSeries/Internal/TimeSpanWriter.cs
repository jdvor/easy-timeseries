namespace Easy.TimeSeries;

internal sealed class TimeSpanWriter
{
    private readonly long precision;
    private readonly TimeSpan min;
    private readonly TimeSpan max;
    private readonly Int32Writer i32Writer;

    public TimeSpanWriter(BitWriter bitWriter, TimePrecision precision)
    {
        i32Writer = new Int32Writer(bitWriter);
        this.precision = Util.GetPrecisionDivisor(precision);
        min = Util.GetMinTimeSpan(precision);
        max = Util.GetMaxTimeSpan(precision);
    }

    public void Write(TimeSpan value)
    {
        Expect.Range(value, min, max);

        var timestamp = (int)(value.Ticks / precision);
        i32Writer.Write(timestamp);
    }

    public static int GetSizeHint(int valueCount, TimePrecision precision)
    {
        return precision switch
        {
            // Coarser units yield smaller deltas, so they need less room. Days used to fall into the catch-all
            // and request the millisecond-sized buffer despite producing the smallest output of any precision.
            TimePrecision.Days => valueCount,
            TimePrecision.Seconds => (int)(valueCount * 1.5),
            TimePrecision.TenthsOfSecond => valueCount * 2,
            _ => valueCount * 3,
        };
    }
}
