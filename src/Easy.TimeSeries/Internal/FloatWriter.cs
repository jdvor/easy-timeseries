namespace Easy.TimeSeries;

internal sealed class FloatWriter(BitWriter bitWriter)
{
    private readonly Int32Writer i32Writer = new(bitWriter);

    public void Write(float value)
        => i32Writer.Write(BitConverter.SingleToInt32Bits(value));

    public static int GetSizeHint(int valueCount)
        => Int32Writer.GetSizeHint(valueCount);
}
