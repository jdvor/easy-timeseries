namespace Easy.TimeSeries;

internal sealed class CategoryWriter(BitWriter bitWriter)
{
    public void Write(short id)
    {
        if (id <= 15)
        {
            bitWriter.Write(0, 1);
            bitWriter.Write((ulong)id, 4);
            return;
        }

        bitWriter.Write(1, 1);
        bitWriter.Write((ulong)id, 15);
    }

    public static int GetSizeHint(int valueCount)
        => valueCount + 8;
}
