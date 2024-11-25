namespace Easy.TimeSeries.Tests;

using System.Buffers;
using System.Collections.Immutable;

public class TimeSpanTests
{
    private static readonly ThreadLocal<Random> Rand = new(() => new Random(Environment.TickCount));

    [Theory]
    [InlineData(TimePrecision.Milliseconds)]
    [InlineData(TimePrecision.TenthsOfSecond)]
    [InlineData(TimePrecision.Seconds)]
    public void WriteAndReadBackOnPredefined1(TimePrecision precision)
    {
        WriteAndReadBack(PredefinedData1, precision);
    }

    [Theory]
    [InlineData(TimePrecision.Milliseconds)]
    [InlineData(TimePrecision.TenthsOfSecond)]
    [InlineData(TimePrecision.Seconds)]
    public void WriteAndReadBackOnRandom(TimePrecision precision)
    {
        // Writing random data as time series is not efficient.
        // You should never do it in real life scenario.
        var testData = ImmutableArray.CreateRange(RandomData(20));
        WriteAndReadBack(testData, precision);
    }

    [Theory]
    [InlineData(TimePrecision.Milliseconds)]
    [InlineData(TimePrecision.TenthsOfSecond)]
    [InlineData(TimePrecision.Seconds)]
    public void TooLargeValueThrows(TimePrecision precision)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var max = Util.GetMaxTimeSpan(precision);
            var overMax = max.Add(TimeSpan.FromSeconds(1));

            var bw = new BitWriter(new ArrayBufferWriter<byte>(16));
            var writer = new TimeSpanWriter(bw, precision);
            writer.Write(overMax);
        });
    }

    [Theory]
    [InlineData(TimePrecision.Milliseconds)]
    [InlineData(TimePrecision.TenthsOfSecond)]
    [InlineData(TimePrecision.Seconds)]
    public void TooSmallValueThrows(TimePrecision precision)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var max = Util.GetMinTimeSpan(precision);
            var overMax = max.Add(TimeSpan.FromSeconds(-1));

            var bw = new BitWriter(new ArrayBufferWriter<byte>(16));
            var writer = new TimeSpanWriter(bw, precision);
            writer.Write(overMax);
        });
    }

    private static void WriteAndReadBack(ImmutableArray<TimeSpan> testData, TimePrecision precision)
    {
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new TimeSpanWriter(bw, precision);
        foreach (var value in testData)
        {
            writer.Write(value);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new TimeSpanReader(buffer, precision);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            var isSame = IsSameTime(expected, actual, precision, out var difference);
            Assert.True(isSame, difference);
        }
    }

    // big enough for inefficient storage of random data
    private static int GetSizeHint(ICollection<TimeSpan> testData)
        => (testData.Count / 2 + 0) * sizeof(ulong) + ColumnHeader.Size;

    private static IEnumerable<TimeSpan> RandomData(int n)
    {
        var rand = Rand.Value!;
        return Enumerable.Range(0, n).Select(i => RandTimeSpan(i, rand));

        static TimeSpan RandTimeSpan(int i, Random rand)
            => TimeSpan.FromMilliseconds(rand.Next(i * 10, i * 100_000) + 1);
    }

    private static readonly ImmutableArray<TimeSpan> PredefinedData1 = ImmutableArray.Create(
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(-10),
        TimeSpan.FromMilliseconds(19),
        TimeSpan.FromMilliseconds(789),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(61),
        TimeSpan.FromSeconds(508),
        TimeSpan.FromSeconds(1673),
        TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(272),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
        TimeSpan.FromDays(2),
        TimeSpan.FromDays(24));

    private static bool IsSameTime(
        TimeSpan expected,
        TimeSpan actual,
        TimePrecision precision,
        out string difference)
    {
        var div = Util.GetPrecisionDivisor(precision);
        if (expected.Ticks / div != actual.Ticks / div)
        {
            difference = $"expected: {expected} != actual: {actual}; {precision}";
            return false;
        }

        difference = string.Empty;
        return true;
    }
}
