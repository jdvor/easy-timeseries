namespace Easy.TimeSeries.AzureBlobs.Tests;

using Easy.TimeSeries;
using Easy.TimeSeries.Abstractions;

[Collection(nameof(FixtureCollection))]
public class AzureBlobStorageTests
{
    private readonly Fixture fixture;

    public AzureBlobStorageTests(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Write_then_read_back_round_trips_through_blob()
    {
        var ct = TestContext.Current.CancellationToken;
        var t = new DateTime(2026, 8, 2, 17, 24, 47, 264, DateTimeKind.Utc);
        var readings = SampleReadings(t);

        await using (var writeStorage = fixture.HourFactory.Create(fromUtcInclusive: t))
        {
            using var writer = BuildWriter(readings);
            await writer.WriteToAsync(writeStorage, ct);
        }

        // Read back with a fresh storage instance pointing at the same blob path, proving the data
        // survived the round-trip through Azurite rather than lingering in the writer's buffers.
        await using var readStorage = fixture.HourFactory.Create(fromUtcInclusive: t);
        var result = await Reader.ReadFromAsync(readStorage, new ReflectionBasedMaterializer<Reading>(), null, ct);

        Assert.Equal(readings.Length, result.Length);
        for (var i = 0; i < readings.Length; i++)
        {
            Assert.Equal(readings[i].Timestamp, result[i].Timestamp);
            Assert.Equal(readings[i].Value, result[i].Value);
            Assert.Equal(readings[i].Quality, result[i].Quality);
            Assert.Equal(readings[i].Status, result[i].Status);
        }
    }

    [Fact]
    public async Task Rewriting_the_same_blob_overwrites_previous_content()
    {
        var ct = TestContext.Current.CancellationToken;
        var t = new DateTime(2026, 3, 14, 9, 0, 0, DateTimeKind.Utc);

        var first = SampleReadings(t, count: 8);
        await using (var storage = fixture.DayFactory.Create(fromUtcInclusive: t))
        {
            using var writer = BuildWriter(first);
            await writer.WriteToAsync(storage, ct);
        }

        var second = SampleReadings(t, count: 3);
        await using (var storage = fixture.DayFactory.Create(fromUtcInclusive: t))
        {
            using var writer = BuildWriter(second);
            await writer.WriteToAsync(storage, ct);
        }

        await using var readStorage = fixture.DayFactory.Create(fromUtcInclusive: t);
        var result = await Reader.ReadFromAsync(readStorage, new ReflectionBasedMaterializer<Reading>(), null, ct);

        Assert.Equal(second.Length, result.Length);
        for (var i = 0; i < second.Length; i++)
        {
            Assert.Equal(second[i].Timestamp, result[i].Timestamp);
            Assert.Equal(second[i].Value, result[i].Value);
        }
    }

    private static Writer BuildWriter(Reading[] readings)
        => new Writer(readings.Length)
            .AddTimeOrdered(readings.Select(x => x.Timestamp), nameof(Reading.Timestamp))
            .AddDouble(readings.Select(x => x.Value), nameof(Reading.Value))
            .AddInt32(readings.Select(x => x.Quality), nameof(Reading.Quality))
            .AddCategory(readings.Select(x => x.Status), nameof(Reading.Status));

    private static Reading[] SampleReadings(DateTime start, int count = 5)
    {
        var readings = new Reading[count];
        for (var i = 0; i < count; i++)
        {
            readings[i] = new Reading
            {
                Timestamp = start.AddSeconds(i),
                Value = 100.5 + i,
                Quality = 200 + i,
                Status = i % 2 == 0 ? "ok" : "degraded",
            };
        }

        return readings;
    }

    private sealed class Reading
    {
        [Column(0)]
        public DateTime Timestamp { get; init; }

        [Column(1)]
        public double Value { get; init; }

        [Column(2)]
        public int Quality { get; init; }

        [Column(3)]
        public string Status { get; init; } = string.Empty;
    }
}
