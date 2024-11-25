namespace Easy.TimeSeries.Tests;

using System.Collections.Immutable;

public class DateTimeTests
{
    [Theory]
    [InlineData(TimePrecision.Milliseconds)]
    [InlineData(TimePrecision.TenthsOfSecond)]
    [InlineData(TimePrecision.Seconds)]
    public void WriteAndReadBack1(TimePrecision precision)
    {
        var testData = PredefinedData1();
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new DateTimeWriter(bw, precision);
        foreach (var time in testData)
        {
            writer.Write(time);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new DateTimeReader(buffer, precision);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            var isSame = IsSameTime(expected, actual, precision, out var difference);
            Assert.True(isSame, difference);
        }
    }

    [Theory]
    [InlineData(TimePrecision.Milliseconds, 73)]
    [InlineData(TimePrecision.TenthsOfSecond, 57)]
    [InlineData(TimePrecision.Seconds, 33)]
    public void WrittenSizeReflectsPrecision(TimePrecision precision, int maxSize)
    {
        var testData = PredefinedData1();
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new DateTimeWriter(bw, precision);
        foreach (var time in testData)
        {
            writer.Write(time);
        }

        bw.Flush();
        var size = bp.WrittenSpan.Length;

        Assert.True(size <= maxSize, $"on precision {precision} the size is too large ({size} > {maxSize})");
    }

    private static int GetSizeHint(ICollection<DateTime> testData)
       => testData.Count * sizeof(ulong) + ColumnHeader.Size;

    private static ImmutableArray<DateTime> PredefinedData1()
    {
        return new DateTimeSeriesBuilder()
            .Append("2023-11-23T20:51:17.887Z")
            .AppendIntervals("3s", "3s", "650ms", "794ms", "1ms", "14m", "3m", "3m")
            .AppendIntervals("1m", "2m", "4m", "8m", "12m", "24m", "32m")
            .Build();
    }

    private static bool IsSameTime(
        DateTime expected,
        DateTime actual,
        TimePrecision precision,
        out string difference)
    {
        if (expected.Kind != actual.Kind)
        {
            difference = "not matching DateTime.Kind";
            return false;
        }

        var msExp = ((DateTimeOffset)expected).ToUnixTimeMilliseconds();
        var msAct = ((DateTimeOffset)actual).ToUnixTimeMilliseconds();
        var div = Util.GetPrecisionDivisor(precision);
        if (msExp / div != msAct / div)
        {
            var diff = msExp - msAct;
            difference = diff.ToString();
            return false;
        }

        difference = string.Empty;
        return true;
    }
}
