namespace Easy.TimeSeries;

internal sealed class DoubleWriter(BitWriter bitWriter)
{
    private readonly Int64Writer i64Writer = new(bitWriter);

    public void Write(double value)
        => i64Writer.Write(BitConverter.DoubleToInt64Bits(value));

    public static int GetSizeHint(int valueCount)
        => Int64Writer.GetSizeHint(valueCount);
}
