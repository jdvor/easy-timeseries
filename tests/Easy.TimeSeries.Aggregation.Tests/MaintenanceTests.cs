namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Paths;

public class MaintenanceTests
{
    private static readonly DateTime Day = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    private static readonly AggregationSpec Daily =
        new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).Column("V", Agg.Max);

    private const string DailyFile = "agg-1h-daily/2026/10/25/sensor-a.ets";
    private const string DailyMarker = "dirty/agg-1h-daily/2026/10/25/sensor-a.json";
    private const string FailedMarker = "dirty/failed/agg-1h-daily/2026/10/25/sensor-a.json";

    private readonly ManualClock clock = new(Day.AddHours(1));
    private readonly List<AggregationFailure> failures = [];
    private readonly TestCatalog inner;

    public MaintenanceTests()
    {
        inner = new TestCatalog(clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Marker_is_processed_once_its_span_has_closed()
    {
        var (catalog, maintenance) = Setup(Daily);
        await WriteRawAsync(catalog, 0, [1d]);
        await WriteRawAsync(catalog, 5, [7d]);

        Assert.Equal(new MarkerRunResult(0, 1, 0, 0, 0, 0), await maintenance.ProcessDirtyAsync(Ct));

        clock.Now = Day.AddDays(1).AddMinutes(4);
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).SkippedOpen);

        clock.Now = Day.AddDays(1).AddMinutes(6);
        Assert.Equal(new MarkerRunResult(1, 0, 0, 0, 0, 0), await maintenance.ProcessDirtyAsync(Ct));

        Assert.False(inner.Exists(DailyMarker));
        var output = await Columns.ReadAsync(inner.OpenRead(DailyFile), Ct);
        Assert.Equal([Day, Day.AddHours(5)], output["t"]);
        Assert.Equal([1d, 7d], output["V.Max"]);
        Assert.Empty(failures);
    }

    [Fact]
    public async Task Late_raw_file_recomputes_a_closed_span()
    {
        var (catalog, maintenance) = Setup(Daily);
        await WriteRawAsync(catalog, 0, [1d]);
        clock.Now = Day.AddDays(2);
        await maintenance.ProcessDirtyAsync(Ct);

        await WriteRawAsync(catalog, 7, [9d]);
        Assert.True(inner.Exists(DailyMarker));
        await maintenance.ProcessDirtyAsync(Ct);

        var output = await Columns.ReadAsync(inner.OpenRead(DailyFile), Ct);
        Assert.Equal([1d, 9d], output["V.Max"]);
    }

    [Fact]
    public async Task Raw_write_during_recompute_keeps_the_marker()
    {
        var (catalog, maintenance) = Setup(Daily);
        await WriteRawAsync(catalog, 0, [1d]);
        clock.Now = Day.AddDays(2);

        var interleaved = false;
        inner.BeforeOpenWrite = async path =>
        {
            if (!interleaved && path == DailyFile)
            {
                interleaved = true;
                await WriteRawAsync(catalog, 3, [5d]);
            }
        };

        await maintenance.ProcessDirtyAsync(Ct);

        // The recompute did not see hour 3, so the marker set by its write survived the conditional delete.
        Assert.True(inner.Exists(DailyMarker));
        Assert.Equal([1d], (await Columns.ReadAsync(inner.OpenRead(DailyFile), Ct))["V.Max"]);

        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Processed);
        Assert.False(inner.Exists(DailyMarker));
        Assert.Equal([1d, 5d], (await Columns.ReadAsync(inner.OpenRead(DailyFile), Ct))["V.Max"]);
    }

    [Fact]
    public async Task Failing_marker_is_retried_then_moved_to_failed()
    {
        var broken = new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).Column("Missing");
        var (catalog, maintenance) = Setup(broken);
        await WriteRawAsync(catalog, 0, [1d]);
        clock.Now = Day.AddDays(2);

        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Retried);

        // The retry delay (10 minutes by default) doubles after each failure.
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Deferred);
        clock.Now += TimeSpan.FromMinutes(10);
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Retried);
        clock.Now += TimeSpan.FromMinutes(19);
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Deferred);
        clock.Now += TimeSpan.FromMinutes(1);
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).MovedToFailed);
        Assert.Equal(new MarkerRunResult(0, 0, 0, 0, 0, 0), await maintenance.ProcessDirtyAsync(Ct));

        Assert.False(inner.Exists(DailyMarker));
        Assert.True(inner.Exists(FailedMarker));
        Assert.Equal(
            [AggregationFailureKind.AttemptFailed, AggregationFailureKind.AttemptFailed, AggregationFailureKind.MovedToFailed],
            failures.Select(x => x.Kind));
        Assert.Equal([1, 2, 3], failures.Select(x => x.Attempt));
        Assert.IsType<AggregationException>(failures[^1].Exception);
    }

    [Fact]
    public async Task Raw_write_clears_the_retry_delay()
    {
        var (catalog, maintenance) = Setup(Daily);
        await inner.WriteAsync("raw/2026/10/25/00/sensor-a.ets", new Writer(1).AddTimeOrdered([Day], "t").AddDouble([1d], "Other"));
        await maintenance.MarkDirtyAsync(Daily, "sensor-a", Day, Ct);
        clock.Now = Day.AddDays(2);
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Retried);
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Deferred);

        // The bad raw file is replaced through the hook, which resets the marker.
        await WriteRawAsync(catalog, 0, [4d]);

        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Processed);
        Assert.Equal([4d], (await Columns.ReadAsync(inner.OpenRead(DailyFile), Ct))["V.Max"]);
    }

    [Fact]
    public async Task Later_raw_write_rearms_a_failed_span_and_success_clears_the_failed_copy()
    {
        var (catalog, maintenance) = Setup(Daily);
        await inner.WriteAsync("raw/2026/10/25/00/sensor-a.ets", new Writer(1).AddTimeOrdered([Day], "t").AddDouble([1d], "Other"));
        await maintenance.MarkDirtyAsync(Daily, "sensor-a", Day, Ct);
        clock.Now = Day.AddDays(2);
        for (var i = 0; i < 3; i++)
        {
            // Past the doubling retry delay of the previous failure.
            clock.Now += TimeSpan.FromHours(1);
            await maintenance.ProcessDirtyAsync(Ct);
        }

        Assert.True(inner.Exists(FailedMarker));

        // The bad raw file is replaced through the hook.
        await WriteRawAsync(catalog, 0, [4d]);
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Processed);

        Assert.False(inner.Exists(FailedMarker));
        Assert.Equal([4d], (await Columns.ReadAsync(inner.OpenRead(DailyFile), Ct))["V.Max"]);
    }

    [Fact]
    public async Task Marker_beyond_raw_retention_is_rejected()
    {
        var (catalog, maintenance) = Setup(Daily);
        await WriteRawAsync(catalog, 0, [1d]);
        clock.Now = Day.AddDays(40);

        Assert.Equal(new MarkerRunResult(0, 0, 0, 0, 1, 0), await maintenance.ProcessDirtyAsync(Ct));

        Assert.False(inner.Exists(DailyMarker));
        Assert.False(inner.Exists(DailyFile));
        Assert.Equal(AggregationFailureKind.RejectedByRetention, Assert.Single(failures).Kind);
    }

    [Fact]
    public async Task Markers_are_processed_in_parallel()
    {
        var (catalog, maintenance) = Setup(Daily, parallelism: 4);
        foreach (var subject in Enumerable.Range(0, 20).Select(i => $"s{i}"))
        {
            await using var storage = catalog.OpenRawWrite(Day.AddHours(2), subject);
            using var writer = new Writer(1).AddTimeOrdered([Day.AddHours(2)], "t").AddDouble([2d], "V");
            await writer.WriteToAsync(storage, Ct);
        }

        clock.Now = Day.AddDays(2);
        Assert.Equal(20, (await maintenance.ProcessDirtyAsync(Ct)).Processed);
        Assert.Equal(20, inner.Paths("agg-1h-daily/").Count(p => p.EndsWith(".ets", StringComparison.Ordinal)));
        Assert.Empty(inner.Paths("dirty/"));
    }

    [Fact]
    public async Task Markers_of_one_span_share_one_raw_listing()
    {
        var (catalog, maintenance) = Setup(Daily, parallelism: 4);
        for (var i = 0; i < 20; i++)
        {
            foreach (var hour in new[] { 1, 2 })
            {
                await using var storage = catalog.OpenRawWrite(Day.AddHours(hour), $"s{i}");
                using var writer = new Writer(1).AddTimeOrdered([Day.AddHours(hour)], "t").AddDouble([i + hour], "V");
                await writer.WriteToAsync(storage, Ct);
            }
        }

        clock.Now = Day.AddDays(2);
        inner.Listed.Clear();
        Assert.Equal(20, (await maintenance.ProcessDirtyAsync(Ct)).Processed);

        Assert.Single(inner.Listed, prefix => prefix.StartsWith("raw/", StringComparison.Ordinal));
        for (var i = 0; i < 20; i++)
        {
            var output = await Columns.ReadAsync(inner.OpenRead($"agg-1h-daily/2026/10/25/s{i}.ets"), Ct);
            Assert.Equal([i + 1d, i + 2d], output["V.Max"]);
        }
    }

    private (AggregatingCatalog Catalog, AggregationMaintenance Maintenance) Setup(AggregationSpec spec, int parallelism = 1)
    {
        var configuration = new AggregationConfiguration
        {
            Raw = new PathBuilder("raw", TimeGranularity.Hour),
            Specs = [spec],
            RawRetention = TimeSpan.FromDays(30),
            TimeProvider = clock,
            MaxDegreeOfParallelism = parallelism,
            OnFailure = failure =>
            {
                lock (failures)
                {
                    failures.Add(failure);
                }
            },
        };
        return (new AggregatingCatalog(inner, configuration), new AggregationMaintenance(inner, configuration));
    }

    private static async Task WriteRawAsync(AggregatingCatalog catalog, int hour, double[] values)
    {
        var at = Day.AddHours(hour);
        await using var storage = catalog.OpenRawWrite(at, "sensor-a");
        using var writer = new Writer(values.Length)
            .AddTimeOrdered(values.Select((_, i) => at.AddSeconds(i)), "t")
            .AddDouble(values, "V");
        await writer.WriteToAsync(storage, Ct);
    }
}
