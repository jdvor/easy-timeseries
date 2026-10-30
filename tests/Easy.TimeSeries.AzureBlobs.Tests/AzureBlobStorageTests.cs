namespace Easy.TimeSeries.AzureBlobs.Tests;

using Easy.TimeSeries;
using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Paths;

[Collection(nameof(FixtureCollection))]
public class AzureBlobStorageTests
{
    private readonly AzureBlobCatalog catalog;

    public AzureBlobStorageTests(Fixture fixture)
    {
        catalog = new AzureBlobCatalog(fixture.CreateContainer());
    }

    [Fact]
    public async Task Write_then_read_back_round_trips_through_blob()
    {
        var ct = TestContext.Current.CancellationToken;
        var paths = new PathBuilder("raw", TimeGranularity.Hour);
        var t = new DateTime(2026, 8, 2, 17, 24, 47, 264, DateTimeKind.Utc);
        var path = paths.GetFilePath(paths.GetBucketStart(t), "sensor-a");
        var readings = SampleReadings(t);

        await using (var writeStorage = catalog.OpenWrite(path))
        {
            using var writer = BuildWriter(readings);
            await writer.WriteToAsync(writeStorage, ct);
        }

        // Read back through a fresh storage, proving the data survived the round-trip through Azurite rather than
        // lingering in the writer's buffers.
        var result = await Reader.ReadFromAsync(catalog.OpenRead(path), new ReflectionBasedMaterializer<Reading>(), null, ct);

        Assert.Equal("raw/2026/08/02/17/sensor-a.ets", path);
        Assert.Equal(readings.Length, result.Length);
        for (var i = 0; i < readings.Length; i++)
        {
            Assert.Equal(readings[i].Timestamp, result[i].Timestamp);
            Assert.Equal(readings[i].Value, result[i].Value);
            Assert.Equal(readings[i].Quality, result[i].Quality);
            Assert.Equal(readings[i].Status, result[i].Status);
        }

        var entries = new List<StorageEntry>();
        await foreach (var entry in catalog.ListAsync(paths.GetExpectedPaths(t, t.AddHours(1)).prefix, ct))
        {
            entries.Add(entry);
        }

        var listed = Assert.Single(entries);
        Assert.True(paths.TryParse(listed.Path, out var bucketStart, out var subjectId));
        Assert.Equal(paths.GetBucketStart(t), bucketStart);
        Assert.Equal("sensor-a", subjectId);
    }

    [Fact]
    public async Task Rewriting_the_same_blob_overwrites_previous_content()
    {
        var ct = TestContext.Current.CancellationToken;
        var t = new DateTime(2026, 3, 14, 9, 0, 0, DateTimeKind.Utc);
        const string path = "raw/2026/03/14/sensor-a.ets";

        var first = SampleReadings(t, count: 8);
        await using (var storage = catalog.OpenWrite(path))
        {
            using var writer = BuildWriter(first);
            await writer.WriteToAsync(storage, ct);
        }

        var second = SampleReadings(t, count: 3);
        await using (var storage = catalog.OpenWrite(path))
        {
            using var writer = BuildWriter(second);
            await writer.WriteToAsync(storage, ct);
        }

        var result = await Reader.ReadFromAsync(catalog.OpenRead(path), new ReflectionBasedMaterializer<Reading>(), null, ct);

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
