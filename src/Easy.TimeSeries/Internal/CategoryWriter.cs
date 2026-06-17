namespace Easy.TimeSeries;

internal sealed class CategoryWriter(BitWriter bitWriter)
{
    public void Write(short id)
    {
        if (id <= 15)
        {
            bitWriter.Write((ulong)id << 1, 5); // bit 0 = 0 (small), bits 1-4 = id
        }
        else
        {
            bitWriter.Write(1UL | ((ulong)id << 1), 16); // bit 0 = 1 (large), bits 1-15 = id
        }

        bitWriter.CommitRecord();
    }

    public static int GetSizeHint(int valueCount)
        => valueCount + 8;
}
