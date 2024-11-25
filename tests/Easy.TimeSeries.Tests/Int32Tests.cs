namespace Easy.TimeSeries.Tests;

using System.Collections.Immutable;

public class Int32Tests
{
    private static readonly ThreadLocal<Random> Rand = new(() => new Random(Environment.TickCount));

    [Fact]
    public void WriteAndReadBackOnPredefined1()
    {
        var testData = ImmutableArray.Create(PredefinedData1);
        WriteAndReadBack(testData);
    }

    [Fact]
    public void WriteAndReadBackOnPredefined2()
    {
        var testData = ImmutableArray.Create(PredefinedData2);
        WriteAndReadBack(testData);
    }

    [Fact]
    public void WriteAndReadBackOnRandom()
    {
        // Writing random data as time series is not efficient.
        // You should never do it in real life scenario.
        var testData = ImmutableArray.CreateRange(RandomData(20));
        WriteAndReadBack(testData);
    }

    private static void WriteAndReadBack(ImmutableArray<int> testData)
    {
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new Int32Writer(bw);
        foreach (var value in testData)
        {
            writer.Write(value);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new Int32Reader(buffer);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            Assert.Equal(expected, actual);
        }
    }

    // big enough for inefficient storage of random data
    private static int GetSizeHint(ICollection<int> testData)
        => (testData.Count + 3) * sizeof(ulong);

    private static IEnumerable<int> RandomData(int n)
    {
        var rand = Rand.Value!;
        return Enumerable.Range(0, n).Select(_ => rand.Next());
    }

    private static readonly int[] PredefinedData1 =
    {
        int.MinValue, int.MinValue + 1, int.MinValue + 2,
        -1, 0, 1,
        int.MaxValue -2, int.MaxValue -1, int.MaxValue,
    };

    private static readonly int[] PredefinedData2 =
    {
        1001, 1001, 1200, 1250, 1500, 1400, 800, 800, 1270, 1271, 1273, 5000, 5050, 1200, 1201, 1202,
    };
}
