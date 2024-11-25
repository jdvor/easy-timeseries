namespace Easy.TimeSeries;

internal sealed class ScaledNumber64Writer(BitWriter bitWriter, int scale)
{
    private readonly Int64Writer i64Writer = new(bitWriter);

    public void Write(double value)
    {
        var v = (long)Math.Floor(value * scale);
        i64Writer.Write(v);
    }

    public static int GetSizeHint(int valueCount)
        => Int64Writer.GetSizeHint(valueCount);
}
