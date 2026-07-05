namespace Easy.TimeSeries;

internal ref struct DateTimeUnorderedReader
{
    private readonly long precision;
    private BitReader bitReader;
    private Xor64Reader xor;

    public readonly ColumnHeader ColumnHeader => bitReader.ColumnHeader;

    public DateTimeUnorderedReader(ReadOnlySpan<byte> buffer, TimePrecision precision)
    {
        bitReader = new BitReader(buffer);
        this.precision = Util.GetPrecisionDivisor(precision);
    }

    public DateTime Read()
    {
        var timestamp = xor.Read(ref bitReader);
        return DateTimeOrderedReader.GetDateTime(timestamp, precision);
    }
}
