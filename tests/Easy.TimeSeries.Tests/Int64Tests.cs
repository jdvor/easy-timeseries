namespace Easy.TimeSeries.Tests;

using System.Buffers;
using System.Collections.Immutable;

public class Int64Tests
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

    private static void WriteAndReadBack(ImmutableArray<long> testData)
    {
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new Int64Writer(bw);
        foreach (var value in testData)
        {
            writer.Write(value);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new Int64Reader(buffer);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            Assert.Equal(expected, actual);
        }
    }

    // big enough for inefficient storage of random data
    private static int GetSizeHint(ICollection<long> testData)
        => (testData.Count + 3) * sizeof(ulong);

    private static IEnumerable<long> RandomData(int n)
    {
        var rand = Rand.Value!;
        return Enumerable.Range(0, n).Select(_ => rand.NextInt64());
    }

    private static readonly long[] PredefinedData1 =
    {
        long.MinValue, long.MinValue + 1, long.MinValue + 2,
        -1, 0, 1,
        long.MaxValue -2, long.MaxValue -1, long.MaxValue,
    };

    private static readonly long[] PredefinedData2 =
    {
        1001, 1001, 1200, 1250, 1500, 1400, 800, 800, 1270, 1271, 1273, 5000, 5050, 1200, 1201, 1202,
    };
}
