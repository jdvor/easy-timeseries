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
            TimePrecision.Seconds => (int)(valueCount * 1.5),
            TimePrecision.TenthsOfSecond => valueCount * 2,
            _ => valueCount * 3,
        };
    }
}
