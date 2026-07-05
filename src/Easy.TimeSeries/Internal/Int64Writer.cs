namespace Easy.TimeSeries;

internal sealed class Int64Writer(BitWriter bitWriter)
{
    private Xor64Writer xor;

    public void Write(long value)
    {
        xor.Write(bitWriter, value);
        bitWriter.CommitRecord();
    }

    public static int GetSizeHint(int valueCount)
        => valueCount * 4;
}
