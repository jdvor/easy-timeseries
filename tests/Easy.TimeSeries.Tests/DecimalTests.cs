namespace Easy.TimeSeries.Tests;

using System.Collections.Immutable;

public class DecimalTests
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
    public void WriteAndReadBackOnSignFlips()
    {
        var testData = ImmutableArray.Create(SignFlipData);
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

    [Fact]
    public void RoundTripPreservesNonCanonicalScale()
    {
        // 1m, 1.0m and 1.00m are equal but have distinct binary representations (scale 0, 1, 2);
        // the round-trip must preserve the exact representation, not just numeric equality.
        var testData = ImmutableArray.Create(1m, 1.0m, 1.00m, 0m, 0.000m);
        WriteAndReadBack(testData);
    }

    private static void WriteAndReadBack(ImmutableArray<decimal> testData)
    {
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new DecimalWriter(bw);
        foreach (var value in testData)
        {
            writer.Write(value);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new DecimalReader(buffer);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            AssertBitExact(expected, actual);
        }
    }

    // Assert.Equal on decimal treats 1.0m and 1.00m as equal; compare the binary representation
    // to verify the exact round-trip including scale.
    private static void AssertBitExact(decimal expected, decimal actual)
    {
        Assert.Equal(decimal.GetBits(expected), decimal.GetBits(actual));
    }

    // big enough for inefficient storage of random data
    private static int GetSizeHint(ICollection<decimal> testData)
        => testData.Count * 2 * sizeof(ulong) + 4;

    private static IEnumerable<decimal> RandomData(int n)
    {
        var rand = Rand.Value!;
        return Enumerable.Range(0, n).Select(_ => new decimal(
            rand.Next(),
            rand.Next(),
            rand.Next(),
            rand.Next(2) == 0,
            (byte)rand.Next(29)));
    }

    // extremes: full 96-bit mantissa, max scale, values crossing the 64-bit mantissa boundary
    private static readonly decimal[] PredefinedData1 =
    {
        decimal.MinValue, decimal.MinValue + 666.56m, decimal.MinValue + 1243.56m,
        -1m, 0m, 1m,
        0.0000000000000000000000000001m, -0.0000000000000000000000000001m,
        18446744073709551615m, 18446744073709551616m, // 2^64 - 1 and 2^64 (hi part becomes non-zero)
        decimal.MaxValue - 1243.56m, decimal.MaxValue - 666.56m, decimal.MaxValue,
    };

    // realistic series: constant number of decimal places, close consecutive values
    private static readonly decimal[] PredefinedData2 =
    {
        891.40m, 892.00m, 890.00m, 1103.68m, 1132.20m, 1000.00m, 1003.50m, 671.10m, 748.80m, 817.66m, 835.00m,
    };

    private static readonly decimal[] SignFlipData =
    {
        0.05m, -0.05m, 0.04m, -0.03m, 0.00m, -0.01m, 0.02m, -0.02m,
    };
}
