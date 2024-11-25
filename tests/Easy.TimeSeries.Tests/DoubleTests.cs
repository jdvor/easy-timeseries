namespace Easy.TimeSeries.Tests;

using System.Collections.Immutable;

public class DoubleTests
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

    private static void WriteAndReadBack(ImmutableArray<double> testData)
    {
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new DoubleWriter(bw);
        foreach (var value in testData)
        {
            writer.Write(value);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new DoubleReader(buffer);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void SizeTest()
    {
        var testData = Enumerable.Range(0, 100).Select(_ => PredefinedData2).SelectMany(x => x).ToArray();

        var (bp, bw) = BufferUtil.CreateBitWriter(testData.Length);

        var writer = new DoubleWriter(bw);
        foreach (var value in testData)
        {
            writer.Write(value);
        }

        bw.Flush();
        var sizeN = testData.Length;
        var sizeB = bp.WrittenSpan.Length;
    }

    // big enough for inefficient storage of random data
    private static int GetSizeHint(ICollection<double> testData)
        => testData.Count * sizeof(ulong) + 2;

    private static IEnumerable<double> RandomData(int n)
    {
        var rand = Rand.Value!;
        return Enumerable.Range(0, n).Select(_ => rand.NextDouble());
    }

    private static readonly double[] PredefinedData1 =
    {
        double.MinValue, double.MinValue + 666.56f, double.MinValue + 1243.56f,
        -1, 0, 1, double.NaN, double.NegativeInfinity, double.PositiveInfinity,
        double.MaxValue - 1243.56f, double.MaxValue - 666.56f, double.MaxValue,
    };

    private static readonly double[] PredefinedData2 =
    {
        891.4d, 892d, 890d, 1103.676d, 1132.2d, 1000d, 1003.5d, 671.1d, 748.8d, 817.66d, 835d,
    };
}
