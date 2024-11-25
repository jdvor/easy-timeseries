namespace Easy.TimeSeries;

internal ref struct TimeSpanReader
{
    private readonly long precision;
    private Int32Reader i32Reader;

    public readonly ColumnHeader ColumnHeader => i32Reader.ColumnHeader;

    public TimeSpanReader(ReadOnlySpan<byte> buffer, TimePrecision precision)
    {
        i32Reader = new Int32Reader(buffer);
        this.precision = Util.GetPrecisionDivisor(precision);
    }

    public TimeSpan Read()
    {
        var i32 = i32Reader.Read();
        return TimeSpan.FromTicks(i32 * precision);
    }
}
