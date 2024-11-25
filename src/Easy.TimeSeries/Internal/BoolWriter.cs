namespace Easy.TimeSeries;

internal sealed class BoolWriter
{
    private readonly BitWriter bitWriter;

    public BoolWriter(BitWriter bitWriter)
    {
        this.bitWriter = bitWriter;
    }

    public void Write(bool value)
    {
        bitWriter.Write(value ? 1 : 0, 1);
    }

    public static int GetSizeHint(int valueCount)
        => valueCount / Constants.WordBitSize;
}
