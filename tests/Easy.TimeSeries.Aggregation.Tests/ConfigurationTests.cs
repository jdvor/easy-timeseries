namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Aggregation.WriteTime;
using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;

public class ConfigurationTests
{
    private static readonly AggregationSpec Hourly = new AggregationSpec(AggregationTimeSpan.TenSeconds, TimeGranularity.Hour).Column("V");
    private static readonly AggregationSpec Daily = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Day).Column("V");

    private static AggregationConfiguration Config(params AggregationSpec[] specs) => new()
    {
        Raw = new PathBuilder("raw", TimeGranularity.Hour),
        Specs = specs,
        RawRetention = TimeSpan.FromDays(30),
    };

    [Fact]
    public void Valid_configuration_passes()
    {
        Assert.NotNull(Config(Hourly, Daily).Validate());
    }

    [Fact]
    public void Storage_time_span_shorter_than_raw_fails()
    {
        var config = Config(Hourly) with { Raw = new PathBuilder("raw", TimeGranularity.Day) };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void Roots_must_not_overlap()
    {
        Assert.Throws<ArgumentException>(() => Config(Daily, Daily.Column("W")).Validate());
        Assert.Throws<ArgumentException>(() => (Config(Daily) with { MarkerRoot = "raw/dirty" }).Validate());
        Assert.Throws<ArgumentException>(() => (Config(Daily) with { MarkerRoot = "agg-1m-daily" }).Validate());
        Assert.Throws<ArgumentException>(() => Config(Daily.WithRoot("raw/agg")).Validate());

        // A shared name prefix is not a shared folder.
        Assert.NotNull((Config(Daily) with { MarkerRoot = "raw-dirty" }).Validate());
    }

    [Fact]
    public void Spec_roots_must_not_start_with_the_failed_marker_folder()
    {
        Assert.Throws<ArgumentException>(() => Config(Daily.WithRoot("failed")).Validate());
        Assert.Throws<ArgumentException>(() => Config(Daily.WithRoot("foo"), Daily.WithRoot("failed/foo")).Validate());
        Assert.NotNull(Config(Daily.WithRoot("failed-agg")).Validate());
    }

    [Fact]
    public void Roots_that_look_like_dates_fail_with_an_empty_raw_root()
    {
        var config = Config(Daily.WithRoot("2026")) with { Raw = new PathBuilder(TimeGranularity.Hour) };
        Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.NotNull((Config(Daily) with { Raw = new PathBuilder(TimeGranularity.Hour) }).Validate());
    }

    [Fact]
    public void Limits_are_checked()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => (Config(Daily) with { MaxAttempts = 0 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (Config(Daily) with { MaxDegreeOfParallelism = 0 }).Validate());
        Assert.Throws<ArgumentException>(() => (Config(Daily) with { RawRetention = TimeSpan.Zero }).Validate());
        Assert.Throws<ArgumentException>(() => (Config(Daily) with { OrphanedTempFileAge = TimeSpan.FromSeconds(-1) }).Validate());
        Assert.Throws<ArgumentException>(() => (Config(Daily) with { RetryDelay = TimeSpan.FromSeconds(-1) }).Validate());
        Assert.NotNull((Config(Daily) with { RawRetention = Timeout.InfiniteTimeSpan }).Validate());
    }

    [Fact]
    public void Raw_retention_must_outlast_the_longest_storage_time_span()
    {
        var monthly = new AggregationSpec(AggregationTimeSpan.FifteenMinutes, TimeGranularity.Month).Column("V");
        Assert.Throws<ArgumentException>(() => Config(monthly).Validate());
        Assert.NotNull((Config(monthly) with { RawRetention = TimeSpan.FromDays(32) }).Validate());
        Assert.Throws<ArgumentException>(() => (Config(Daily) with { RawRetention = TimeSpan.FromDays(1) }).Validate());
    }

    [Fact]
    public void Throwing_callback_is_ignored()
    {
        var config = Config(Daily) with { OnFailure = _ => throw new InvalidOperationException("boom") };
        config.Report(new AggregationFailure(AggregationFailureKind.AttemptFailed, Daily, "s", DateTime.UnixEpoch, null, 1, null));
    }

    [Fact]
    public async Task Markers_round_trip_with_paths_per_spec()
    {
        var ct = TestContext.Current.CancellationToken;
        var catalog = new InMemoryCatalog();
        var config = Config(Hourly, Daily).Validate();
        var store = new MarkerStore(catalog, config);
        var day = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

        await store.SetAsync(Daily, "sensor-a", day, ct);
        await store.SetAsync(Hourly, "sensor-a", day.AddHours(14), ct);

        Assert.Equal("dirty/agg-1m-daily/2026/10/25/sensor-a.json", store.PathOf(Daily, "sensor-a", day));
        Assert.Equal("dirty/failed/agg-1m-daily/2026/10/25/sensor-a.json", store.FailedPathOf(Daily, "sensor-a", day));

        var markers = new List<Marker>();
        await foreach (var marker in store.ListAsync(ct))
        {
            markers.Add(marker);
        }

        Assert.Equal(2, markers.Count);
        var daily = Assert.Single(markers, m => ReferenceEquals(m.Spec, Daily));
        Assert.Equal(("sensor-a", day), (daily.SubjectId, daily.SpanStart));

        var content = await store.ReadAsync(daily.Entry.Path, ct);
        Assert.Equal((0, null, 1), (content.Attempts, content.LastError, content.FormatVersion));

        await store.WriteAsync(daily.Entry.Path, content with { Attempts = 1, LastError = "x" }, daily.Entry.Version, ct);
        Assert.Equal(1, (await store.ReadAsync(daily.Entry.Path, ct)).Attempts);
        await Assert.ThrowsAsync<Abstractions.StorageVersionMismatchException>(
            () => store.WriteAsync(daily.Entry.Path, content, daily.Entry.Version, ct));
    }

    [Fact]
    public async Task Unreadable_marker_counts_as_fresh()
    {
        var ct = TestContext.Current.CancellationToken;
        var catalog = new InMemoryCatalog();
        var store = new MarkerStore(catalog, Config(Daily).Validate());
        await using (var storage = catalog.OpenWrite("dirty/agg-1m-daily/2026/10/25/s.json"))
        {
            await storage.WriteAsync("not json"u8.ToArray(), ct);
            await storage.CloseAsync(ct);
        }

        Assert.Equal(0, (await store.ReadAsync("dirty/agg-1m-daily/2026/10/25/s.json", ct)).Attempts);
        Assert.Equal(0, (await store.ReadAsync("dirty/agg-1m-daily/2026/10/25/missing.json", ct)).Attempts);
    }
}
