namespace Easy.TimeSeries.Tests;

using System.Collections.Immutable;

public class FloatTests
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

    private static void WriteAndReadBack(ImmutableArray<float> testData)
    {
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new FloatWriter(bw);
        foreach (var value in testData)
        {
            writer.Write(value);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new FloatReader(buffer);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            Assert.Equal(expected, actual);
        }
    }

    // big enough for inefficient storage of random data
    private static int GetSizeHint(ICollection<float> testData)
        => testData.Count * sizeof(ulong) + 2;

    private static IEnumerable<float> RandomData(int n)
    {
        var rand = Rand.Value!;
        return Enumerable.Range(0, n).Select(_ => rand.NextSingle());
    }

    private static readonly float[] PredefinedData1 =
    {
        float.MinValue, float.MinValue + 666.56f, float.MinValue + 1243.56f,
        -1, 0, 1,
        float.MaxValue - 1243.56f, float.MaxValue - 666.56f, float.MaxValue,
    };

    private static readonly float[] PredefinedData2 =
    {
        891.4f, 892f, 890f, 1103.676f, 1132.2f, 1000f, 1003.5f, 671.1f, 748.8f, 817.66f, 835f,
    };
}
