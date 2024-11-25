namespace Easy.TimeSeries.Tests;

public class BitsTests
{
    [Fact]
    public void WriteAndReadBack()
    {
        var (bp, writer) = BufferUtil.CreateBitWriter(GetSizeHint(TestData));

        foreach (var (value, bits) in TestData)
        {
            writer.Write(value, bits);
        }

        writer.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new BitReader(buffer);
        foreach (var (expected, bits) in TestData)
        {
            var actual = reader.Read(bits);
            Assert.Equal(expected, actual);
        }
    }

    private static int GetSizeHint<T>(ICollection<T> testData)
        => ColumnHeader.Size + testData.Count * Constants.WordByteSize;

    private static readonly (ulong Value, int Bits)[] TestData =
    {
        (0b0000_0001, 1),
        (0b0000_1001, 4),
        (0b0000_0101, 3),
        (0b0001_0101, 5),
        (0b0101_0101, 7),
        (0b0001_0001, 8),
        (0b0000_0000, 1),
        (0b1000_0001, 8),
        (0b1111_1110, 8),
    };
}
