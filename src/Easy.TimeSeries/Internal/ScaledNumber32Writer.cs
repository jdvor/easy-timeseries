namespace Easy.TimeSeries;

internal sealed class ScaledNumber32Writer(BitWriter bitWriter, int scale)
{
    private readonly Int32Writer i32Writer = new(bitWriter);

    public void Write(double value)
    {
        var v = (int)Math.Floor(value * scale);
        i32Writer.Write(v);
    }

    public static int GetSizeHint(int valueCount)
        => Int32Writer.GetSizeHint(valueCount);
}
