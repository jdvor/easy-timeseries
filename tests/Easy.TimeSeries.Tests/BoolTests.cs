namespace Easy.TimeSeries.Tests;

using System.Collections.Immutable;
using System.Security.Cryptography;

public class BoolTests
{
    [Fact]
    public void WriteAndReadBackOnPredefined1()
    {
        var testData = TestData(264);
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new BoolWriter(bw);
        foreach (var value in testData)
        {
            writer.Write(value);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new BoolReader(buffer);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            Assert.Equal(expected, actual);
        }
    }

    private static int GetSizeHint(ICollection<bool> testData)
        => testData.Count / sizeof(ulong) + 1 + ColumnHeader.Size;

    private static ImmutableArray<bool> TestData(int n)
    {
        var values = new bool[n];
        for (var i = 0; i < n; i++)
        {
            values[i] = RandomNumberGenerator.GetInt32(0, 2) == 0;
        }

        return ImmutableArray.Create(values);
    }
}
