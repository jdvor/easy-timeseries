namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Paths;

public class AggregatingCatalogTests
{
    private static readonly DateTime Hour14 = new(2026, 10, 25, 14, 0, 0, DateTimeKind.Utc);

    private static readonly AggregationSpec Hourly =
        new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour).Column("V", Agg.Avg);

    private static readonly AggregationSpec Daily =
        new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).Column("V", Agg.Max);

    private readonly List<AggregationFailure> failures = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Raw_write_aggregates_equal_spans_and_marks_longer_ones()
    {
        var inner = new TestCatalog();
        var catalog = new AggregatingCatalog(inner, Config(Hourly, Daily));

        await WriteRawAsync(catalog.OpenRawWrite(Hour14, "sensor-a"), [1d, 3d]);

        Assert.Equal(
            [
                "agg-1m-hourly/2026/10/25/14/sensor-a.ets",
                "agg-1m-hourly/2026/10/25/14/sensor-a.sources.json",
                "dirty/agg-1h-daily/2026/10/25/sensor-a.json",
                "raw/2026/10/25/14/sensor-a.ets",
            ],
            inner.Paths());
        var hourly = await Columns.ReadAsync(inner.OpenRead("agg-1m-hourly/2026/10/25/14/sensor-a.ets"), Ct);
        Assert.Equal([2d], hourly["V.Avg"]);
        Assert.Empty(failures);
    }

    [Fact]
    public async Task Paths_that_are_not_raw_pass_through()
    {
        var inner = new TestCatalog();
        var catalog = new AggregatingCatalog(inner, Config(Hourly, Daily));

        await WriteRawAsync(catalog.OpenWrite("other/2026/10/25/14/sensor-a.ets"), [1d]);

        Assert.Equal(["other/2026/10/25/14/sensor-a.ets"], inner.Paths());
    }

    [Fact]
    public async Task Aborted_raw_write_aggregates_nothing()
    {
        var inner = new TestCatalog();
        var catalog = new AggregatingCatalog(inner, Config(Hourly, Daily));

        await using (var storage = catalog.OpenRawWrite(Hour14, "sensor-a"))
        {
            await storage.WriteAsync(new byte[] { 1, 2, 3 }, Ct);
        }

        Assert.Empty(inner.Paths());
    }

    [Fact]
    public async Task Failed_immediate_aggregation_sets_a_marker_and_close_succeeds()
    {
        var inner = new TestCatalog();
        var broken = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour).Column("Missing");
        var catalog = new AggregatingCatalog(inner, Config(broken));

        await WriteRawAsync(catalog.OpenRawWrite(Hour14, "sensor-a"), [1d]);

        Assert.True(inner.Exists("raw/2026/10/25/14/sensor-a.ets"));
        Assert.True(inner.Exists("dirty/agg-1m-hourly/2026/10/25/14/sensor-a.json"));
        var failure = Assert.Single(failures);
        Assert.Equal(AggregationFailureKind.ImmediateFailed, failure.Kind);
        Assert.Equal("raw/2026/10/25/14/sensor-a.ets", failure.RawPath);
        Assert.IsType<AggregationException>(failure.Exception);
    }

    [Fact]
    public async Task Failed_marker_write_is_reported_and_close_succeeds()
    {
        var inner = new TestCatalog
        {
            BeforeOpenWrite = path => path.StartsWith("dirty/", StringComparison.Ordinal)
                ? throw new IOException("disk full")
                : Task.CompletedTask,
        };
        var catalog = new AggregatingCatalog(inner, Config(Daily));

        await WriteRawAsync(catalog.OpenRawWrite(Hour14, "sensor-a"), [1d]);

        Assert.Equal(["raw/2026/10/25/14/sensor-a.ets"], inner.Paths());
        var failure = Assert.Single(failures);
        Assert.Equal(AggregationFailureKind.MarkerWriteFailed, failure.Kind);
        Assert.Equal(new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc), failure.StorageSpanStart);
    }

    [Fact]
    public async Task Failed_raw_write_propagates_and_aggregates_nothing()
    {
        var inner = new TestCatalog();
        var catalog = new AggregatingCatalog(inner, Config(Hourly));
        await WriteRawAsync(catalog.OpenRawWrite(Hour14, "sensor-a"), [1d]);
        var version = (await inner.GetAsync("raw/2026/10/25/14/sensor-a.ets", Ct))!.Value.Version;
        await WriteRawAsync(catalog.OpenRawWrite(Hour14, "sensor-a"), [5d]);

        await Assert.ThrowsAsync<Abstractions.StorageVersionMismatchException>(
            () => WriteRawAsync(catalog.OpenWrite("raw/2026/10/25/14/sensor-a.ets", version), [9d]));

        var hourly = await Columns.ReadAsync(inner.OpenRead("agg-1m-hourly/2026/10/25/14/sensor-a.ets"), Ct);
        Assert.Equal([5d], hourly["V.Avg"]);
    }

    [Fact]
    public async Task Concurrent_writer_of_the_same_raw_path_sets_a_marker()
    {
        var inner = new TestCatalog();
        var catalog = new AggregatingCatalog(inner, Config(Hourly));
        var interleaved = false;
        inner.BeforeGet = async path =>
        {
            // Another writer commits a longer raw file right after ours.
            if (!interleaved && path.StartsWith("raw/", StringComparison.Ordinal))
            {
                interleaved = true;
                await inner.WriteAsync(path, RawWriter([7d, 8d, 9d]));
            }
        };

        await WriteRawAsync(catalog.OpenRawWrite(Hour14, "sensor-a"), [1d]);

        Assert.True(inner.Exists("dirty/agg-1m-hourly/2026/10/25/14/sensor-a.json"));
        Assert.Empty(failures);
    }

    [Fact]
    public async Task Concurrent_writer_of_equal_size_is_not_mistaken_for_ours()
    {
        var inner = new TestCatalog();
        var catalog = new AggregatingCatalog(inner, Config(Hourly));
        var interleaved = false;
        inner.BeforeGet = async path =>
        {
            // Another writer commits a raw file of the same size right after ours, before the hook looks at storage.
            if (!interleaved && path.StartsWith("raw/", StringComparison.Ordinal))
            {
                interleaved = true;
                await inner.WriteAsync("raw/2026/10/25/14/sensor-a.ets", RawWriter([7d]));
            }
        };

        await WriteRawAsync(catalog.OpenRawWrite(Hour14, "sensor-a"), [1d]);

        Assert.True(interleaved);
        using (var ours = RawWriter([1d]))
        {
            await using var buffer = new Storage.InMemoryStorage();
            await ours.WriteToAsync(buffer, Ct);
            Assert.Equal((await inner.GetAsync("raw/2026/10/25/14/sensor-a.ets", Ct))!.Value.Size, (await buffer.ReadAsync(Ct)).Length);
        }

        Assert.True(inner.Exists("dirty/agg-1m-hourly/2026/10/25/14/sensor-a.json"));
        Assert.False(inner.Exists("agg-1m-hourly/2026/10/25/14/sensor-a.sources.json"));
        Assert.Empty(failures);
    }

    [Fact]
    public async Task Throwing_failure_callback_does_not_break_the_raw_write()
    {
        var inner = new TestCatalog();
        var broken = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour).Column("Missing");
        var catalog = new AggregatingCatalog(inner, Config(broken) with { OnFailure = _ => throw new InvalidOperationException("boom") });

        await WriteRawAsync(catalog.OpenRawWrite(Hour14, "sensor-a"), [1d]);

        Assert.True(inner.Exists("raw/2026/10/25/14/sensor-a.ets"));
        Assert.True(inner.Exists("dirty/agg-1m-hourly/2026/10/25/14/sensor-a.json"));
    }

    private static Writer RawWriter(double[] values)
        => new Writer(values.Length)
            .AddTimeOrdered(values.Select((_, i) => Hour14.AddSeconds(i)), "t")
            .AddDouble(values, "V");

    private static async Task WriteRawAsync(Abstractions.IWriteStorage storage, double[] values)
    {
        await using (storage)
        {
            using var writer = RawWriter(values);
            await writer.WriteToAsync(storage, Ct);
        }
    }

    private AggregationConfiguration Config(params AggregationSpec[] specs) => new()
    {
        Raw = new PathBuilder("raw", TimeGranularity.Hour),
        Specs = specs,
        RawRetention = TimeSpan.FromDays(30),
        OnFailure = failures.Add,
    };
}
