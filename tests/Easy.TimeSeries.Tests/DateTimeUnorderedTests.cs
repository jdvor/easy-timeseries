namespace Easy.TimeSeries.Tests;

using Abstractions;
using Easy.TimeSeries.Storage;
using System.Collections.Immutable;

public class DateTimeUnorderedTests
{
    [Theory]
    [InlineData(TimePrecision.Milliseconds)]
    [InlineData(TimePrecision.TenthsOfSecond)]
    [InlineData(TimePrecision.Seconds)]
    public void WriteAndReadBackUnordered(TimePrecision precision)
    {
        var testData = PredefinedUnordered();
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new DateTimeUnorderedWriter(bw, precision);
        foreach (var time in testData)
        {
            writer.Write(time);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new DateTimeUnorderedReader(buffer, precision);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            Assert.Equal(expected, actual);
            Assert.Equal(DateTimeKind.Utc, actual.Kind);
        }
    }

    [Fact]
    public void WriteAndReadBackMillisecondJitter()
    {
        var testData = new DateTimeSeriesBuilder()
            .AppendDates(
                "2023-11-23T20:51:17.887Z",
                "2023-11-23T20:51:17.884Z",
                "2023-11-23T20:51:18.001Z",
                "2023-11-23T20:51:17.887Z",
                "2023-11-23T20:50:03.120Z",
                "2023-11-24T09:12:44.554Z",
                "2023-11-23T20:51:18.001Z")
            .Build();
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new DateTimeUnorderedWriter(bw, TimePrecision.Milliseconds);
        foreach (var time in testData)
        {
            writer.Write(time);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new DateTimeUnorderedReader(buffer, TimePrecision.Milliseconds);
        foreach (var expected in testData)
        {
            Assert.Equal(expected, reader.Read());
        }
    }

    [Fact]
    public void WriteAndReadBackPreEpochDates()
    {
        // Dates before Epoch (2000-01-01) produce negative timestamps; the ordered
        // delta-of-delta writer cannot store these, the XOR-based one can.
        var testData = new DateTimeSeriesBuilder()
            .AppendDates(
                "1970-05-01T00:00:12Z",
                "1999-12-31T23:59:59Z",
                "1970-05-01T00:00:12Z",
                "2024-02-29T10:00:00Z",
                "1985-07-13T06:30:00Z")
            .Build();
        var (bp, bw) = BufferUtil.CreateBitWriter(GetSizeHint(testData));

        var writer = new DateTimeUnorderedWriter(bw, TimePrecision.Seconds);
        foreach (var time in testData)
        {
            writer.Write(time);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;

        var reader = new DateTimeUnorderedReader(buffer, TimePrecision.Seconds);
        foreach (var expected in testData)
        {
            Assert.Equal(expected, reader.Read());
        }
    }

    [Fact]
    public void ThrowsOnNonUtc()
    {
        var (_, bw) = BufferUtil.CreateBitWriter(64);
        var writer = new DateTimeUnorderedWriter(bw, TimePrecision.Milliseconds);

        Assert.Throws<ArgumentException>(
            () => writer.Write(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local)));
    }

    [Fact]
    public async Task WriteAndReadBackViaMaterializer()
    {
        var ct = TestContext.Current.CancellationToken;
        var events = Delta.DataSet1();

        var writer = new Writer(events.Length)
            .AddTimeUnordered(events.Select(x => x.OccurredAt), "OccurredAt")
            .AddInt32(events.Select(x => x.Value), "Value");

        var storage = new InMemoryStorage();
        await writer.WriteToAsync(storage, ct);

        var materializer = new ReflectionBasedMaterializer<Delta>();
        var result = await Reader.ReadFromAsync(storage, materializer, null, ct);

        Assert.Equal(events.Length, result.Length);
        for (var i = 0; i < events.Length; i++)
        {
            Assert.Equal(events[i].OccurredAt, result[i].OccurredAt);
            Assert.Equal(events[i].Value, result[i].Value);
        }
    }

    private static int GetSizeHint(ICollection<DateTime> testData)
        => (testData.Count * sizeof(ulong)) + ColumnHeader.Size;

    private static ImmutableArray<DateTime> PredefinedUnordered()
    {
        // Whole-second values so all tested precisions round-trip exactly;
        // deliberately out of order with duplicates and adjacent equal values.
        return new DateTimeSeriesBuilder()
            .AppendDates(
                "2023-11-23T20:51:17Z",
                "2023-11-23T20:51:20Z",
                "2023-11-23T20:51:14Z",
                "2023-11-23T20:51:14Z",
                "2023-11-25T04:02:59Z",
                "2023-11-23T20:51:15Z",
                "2023-11-01T00:00:00Z",
                "2023-11-23T20:51:15Z")
            .Build();
    }

    public sealed class Delta
    {
        [Column(0, ValueType = DataValueType.DateTimeUnordered)]
        public DateTime OccurredAt { get; set; }

        [Column(1)]
        public int Value { get; set; }

        public static Delta[] DataSet1()
        {
            return
            [
                new Delta { OccurredAt = Utc("2024-03-01T12:00:00.250Z"), Value = 5 },
                new Delta { OccurredAt = Utc("2024-03-01T11:59:58.001Z"), Value = 7 },
                new Delta { OccurredAt = Utc("2024-03-01T12:00:03.999Z"), Value = 2 },
                new Delta { OccurredAt = Utc("2024-03-01T11:59:58.001Z"), Value = 9 },
                new Delta { OccurredAt = Utc("2024-02-14T08:30:00.000Z"), Value = 1 },
            ];

            static DateTime Utc(string s)
                => DateTime.Parse(s).ToUniversalTime();
        }
    }
}
