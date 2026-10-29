namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;
using System.IO;

public class RecoveryTests
{
    private static readonly DateTime Day = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    private static readonly AggregationSpec Hourly =
        new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour).Column("V", Agg.Avg);

    private static readonly AggregationSpec Daily =
        new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).Column("V", Agg.Max);

    private readonly ManualClock clock = new(Day.AddHours(3));
    private readonly List<AggregationFailure> failures = [];
    private readonly TestCatalog inner;

    public RecoveryTests()
    {
        inner = new TestCatalog(clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Raw_files_written_without_the_hook_get_markers()
    {
        var maintenance = Maintenance(inner);
        for (var hour = 0; hour < 3; hour++)
        {
            await inner.WriteAsync($"raw/2026/10/25/{hour:D2}/sensor-a.ets", Raw(hour, 1d));
        }

        var result = await maintenance.RecoverAsync(Day, Day.AddDays(1), Ct);

        Assert.Equal(new RecoveryResult(RawFilesScanned: 3, MarkersSet: 4, TempFilesRemoved: 0), result);
        Assert.Equal(
            [
                "dirty/agg-1h-daily/2026/10/25/sensor-a.json",
                "dirty/agg-1m-hourly/2026/10/25/00/sensor-a.json",
                "dirty/agg-1m-hourly/2026/10/25/01/sensor-a.json",
                "dirty/agg-1m-hourly/2026/10/25/02/sensor-a.json",
            ],
            inner.Paths("dirty/"));

        // Already dirty: nothing new to mark.
        Assert.Equal(0, (await maintenance.RecoverAsync(Day, Day.AddDays(1), Ct)).MarkersSet);

        clock.Now = Day.AddDays(2);
        Assert.Equal(4, (await maintenance.ProcessDirtyAsync(Ct)).Processed);
        clock.Now = Day.AddDays(3);
        Assert.Equal(0, (await maintenance.RecoverAsync(Day, Day.AddDays(1), Ct)).MarkersSet);
    }

    [Fact]
    public async Task Raw_file_modified_after_its_aggregated_file_is_stale()
    {
        var maintenance = Maintenance(inner);
        var catalog = new AggregatingCatalog(inner, Configuration());
        await using (var storage = catalog.OpenRawWrite(Day, "sensor-a"))
        {
            using var writer = Raw(0, 1d);
            await writer.WriteToAsync(storage, Ct);
        }

        clock.Now = Day.AddDays(2);
        await maintenance.ProcessDirtyAsync(Ct);
        Assert.Empty(inner.Paths("dirty/"));

        clock.Now = Day.AddDays(3);
        await inner.WriteAsync("raw/2026/10/25/00/sensor-a.ets", Raw(0, 2d));

        var result = await maintenance.RecoverAsync(Day.AddHours(-1), Day.AddHours(1).AddMinutes(30), Ct);

        Assert.Equal(new RecoveryResult(1, 2, 0), result);
    }

    [Fact]
    public async Task Aggregated_file_overwritten_after_its_record_is_stale()
    {
        var maintenance = Maintenance(inner, Hourly);
        var catalog = new AggregatingCatalog(inner, Configuration(Hourly));
        await using (var storage = catalog.OpenRawWrite(Day, "sensor-a"))
        {
            using var writer = Raw(0, 1d);
            await writer.WriteToAsync(storage, Ct);
        }

        Assert.Equal(0, (await maintenance.RecoverAsync(Day, Day.AddHours(1), Ct)).MarkersSet);

        // An overlapping run writes its aggregated file over ours; the record stays the one written for ours.
        await inner.WriteAsync(
            "agg-1m-hourly/2026/10/25/00/sensor-a.ets",
            new Writer(1).AddTimeOrdered([Day], "t").AddInt32([1], "Count").AddDouble([5d], "V.Avg"));

        Assert.Equal(1, (await maintenance.RecoverAsync(Day, Day.AddHours(1), Ct)).MarkersSet);
        Assert.True(inner.Exists("dirty/agg-1m-hourly/2026/10/25/00/sensor-a.json"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"raw":null}""")]
    [InlineData("""{"raw":{},"formatVersion":99}""")]
    public async Task Incomplete_source_record_marks_its_file_and_recovery_continues(string json)
    {
        var maintenance = Maintenance(inner, Hourly);
        var catalog = new AggregatingCatalog(inner, Configuration(Hourly));
        foreach (var subject in new[] { "sensor-a", "sensor-b" })
        {
            await using var storage = catalog.OpenRawWrite(Day, subject);
            using var writer = Raw(0, 1d);
            await writer.WriteToAsync(storage, Ct);
        }

        await using (var record = inner.Inner.OpenWrite("agg-1m-hourly/2026/10/25/00/sensor-a.sources.json"))
        {
            await record.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json), Ct);
            await record.CloseAsync(Ct);
        }

        var result = await maintenance.RecoverAsync(Day, Day.AddHours(1), Ct);

        Assert.Equal(new RecoveryResult(RawFilesScanned: 2, MarkersSet: 1, TempFilesRemoved: 0), result);
        Assert.True(inner.Exists("dirty/agg-1m-hourly/2026/10/25/00/sensor-a.json"));
    }

    [Fact]
    public async Task Freshly_computed_files_are_not_stale()
    {
        var catalog = new AggregatingCatalog(inner, Configuration());
        var maintenance = Maintenance(inner);
        for (var hour = 0; hour < 24; hour++)
        {
            // Each raw hour is written as it ends; the hourly file is aggregated right away.
            clock.Now = Day.AddHours(hour + 1);
            await using var storage = catalog.OpenRawWrite(Day.AddHours(hour), "sensor-a");
            using var writer = Raw(hour, hour);
            await writer.WriteToAsync(storage, Ct);
        }

        // The day closes and is computed minutes after its last raw file.
        clock.Now = Day.AddDays(1).AddMinutes(6);
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Processed);

        var result = await maintenance.RecoverAsync(Day, Day.AddDays(1), Ct);

        Assert.Equal(new RecoveryResult(RawFilesScanned: 24, MarkersSet: 0, TempFilesRemoved: 0), result);
        Assert.Empty(inner.Paths("dirty/"));
    }

    [Fact]
    public async Task Raw_file_replaced_without_the_hook_at_the_same_instant_is_stale()
    {
        var catalog = new AggregatingCatalog(inner, Configuration(Hourly));
        await using (var storage = catalog.OpenRawWrite(Day, "sensor-a"))
        {
            using var writer = Raw(0, 1d);
            await writer.WriteToAsync(storage, Ct);
        }

        // Same clock reading as the aggregated file: only the version tells them apart.
        await inner.WriteAsync("raw/2026/10/25/00/sensor-a.ets", Raw(0, 2d));

        var result = await Maintenance(inner, Hourly).RecoverAsync(Day, Day.AddHours(1), Ct);

        Assert.Equal(1, result.MarkersSet);
        Assert.Equal(["dirty/agg-1m-hourly/2026/10/25/00/sensor-a.json"], inner.Paths("dirty/"));
    }

    [Fact]
    public async Task Raw_file_written_without_the_hook_during_a_recompute_is_stale()
    {
        var maintenance = Maintenance(inner, Daily);
        await inner.WriteAsync("raw/2026/10/25/00/sensor-a.ets", Raw(0, 1d));
        await maintenance.MarkDirtyAsync(Daily, "sensor-a", Day, Ct);
        clock.Now = Day.AddDays(2);

        // Lands after the raw listing of a slow recompute, so the aggregated file is well newer than it.
        var interleaved = false;
        inner.BeforeOpenWrite = async path =>
        {
            if (!interleaved && path == "agg-1h-daily/2026/10/25/sensor-a.ets")
            {
                interleaved = true;
                await inner.WriteAsync("raw/2026/10/25/03/sensor-a.ets", Raw(3, 5d));
                clock.Now += TimeSpan.FromMinutes(30);
            }
        };
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Processed);
        Assert.Empty(inner.Paths("dirty/"));

        var result = await maintenance.RecoverAsync(Day, Day.AddDays(1), Ct);

        Assert.Equal(1, result.MarkersSet);
        Assert.Equal(["dirty/agg-1h-daily/2026/10/25/sensor-a.json"], inner.Paths("dirty/"));
    }

    [Fact]
    public async Task Range_is_clipped_to_the_raw_retention()
    {
        var maintenance = Maintenance(inner);
        await inner.WriteAsync("raw/2026/10/25/00/sensor-a.ets", Raw(0, 1d));
        await inner.WriteAsync("raw/2026/11/24/23/sensor-a.ets", Raw(0, 1d, Day.AddDays(30).AddHours(23)));
        clock.Now = Day.AddDays(31);

        var result = await maintenance.RecoverAsync(Day, Day.AddDays(40), Ct);

        Assert.Equal(1, result.RawFilesScanned);
        Assert.All(inner.Paths("dirty/"), path => Assert.Contains("2026/11/24", path, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stale_span_partly_beyond_retention_is_reported_not_marked()
    {
        var maintenance = Maintenance(inner);
        await inner.WriteAsync("raw/2026/10/25/23/sensor-a.ets", Raw(23, 1d));

        // Hour 23 is within retention, but the day started earlier: its first raw hours may be gone.
        clock.Now = Day.AddDays(30).AddHours(12);
        var result = await maintenance.RecoverAsync(Day, Day.AddDays(1), Ct);

        Assert.Equal(new RecoveryResult(1, 1, 0), result);
        Assert.Equal(["dirty/agg-1m-hourly/2026/10/25/23/sensor-a.json"], inner.Paths("dirty/"));
        var rejected = Assert.Single(failures);
        Assert.Equal((AggregationFailureKind.RejectedByRetention, "agg-1h-daily"), (rejected.Kind, rejected.Spec.Root));
    }

    [Fact]
    public async Task Failed_markers_are_listed_and_rearmed()
    {
        var broken = new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).Column("Missing");
        var maintenance = Maintenance(inner, broken);
        await inner.WriteAsync("raw/2026/10/25/00/sensor-a.ets", Raw(0, 1d));
        await maintenance.MarkDirtyAsync(broken, "sensor-a", Day, Ct);
        clock.Now = Day.AddDays(2);
        for (var i = 0; i < 3; i++)
        {
            // Past the doubling retry delay of the previous failure.
            clock.Now += TimeSpan.FromHours(1);
            await maintenance.ProcessDirtyAsync(Ct);
        }

        var failed = await maintenance.ListFailedAsync(Ct).ToListAsync(Ct);

        var marker = Assert.Single(failed);
        Assert.Equal(("sensor-a", Day, 3), (marker.SubjectId, marker.StorageSpanStart, marker.Attempts));
        Assert.Contains("Missing", marker.LastError, StringComparison.Ordinal);
        Assert.Equal(clock.Now, marker.FailedAt);

        await maintenance.RearmAsync(marker, Ct);

        Assert.Equal(["dirty/agg-1h-daily/2026/10/25/sensor-a.json"], inner.Paths("dirty/"));
        Assert.Empty(await maintenance.ListFailedAsync(Ct).ToListAsync(Ct));
    }

    [Fact]
    public async Task Failed_marker_is_not_rearmed_until_a_newer_raw_file_arrives()
    {
        var broken = new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).Column("Missing");
        var maintenance = Maintenance(inner, broken);
        await inner.WriteAsync("raw/2026/10/25/00/sensor-a.ets", Raw(0, 1d));
        await maintenance.MarkDirtyAsync(broken, "sensor-a", Day, Ct);
        clock.Now = Day.AddDays(2);
        for (var i = 0; i < 3; i++)
        {
            // Past the doubling retry delay of the previous failure.
            clock.Now += TimeSpan.FromHours(1);
            await maintenance.ProcessDirtyAsync(Ct);
        }

        clock.Now = Day.AddDays(3);
        Assert.Equal(0, (await maintenance.RecoverAsync(Day, Day.AddDays(1), Ct)).MarkersSet);
        Assert.Equal(["dirty/failed/agg-1h-daily/2026/10/25/sensor-a.json"], inner.Paths("dirty/"));

        // A replaced raw file written without the hook gets a fresh set of attempts.
        await inner.WriteAsync("raw/2026/10/25/00/sensor-a.ets", Raw(0, 2d));

        Assert.Equal(1, (await maintenance.RecoverAsync(Day, Day.AddDays(1), Ct)).MarkersSet);
        Assert.Contains("dirty/agg-1h-daily/2026/10/25/sensor-a.json", inner.Paths("dirty/"));
    }

    [Fact]
    public async Task Mark_dirty_validates_spec_and_span()
    {
        var maintenance = Maintenance(inner);
        await Assert.ThrowsAsync<ArgumentException>(
            () => maintenance.MarkDirtyAsync(Daily, "sensor-a", Day.AddHours(1), Ct));
        await Assert.ThrowsAsync<ArgumentException>(
            () => maintenance.MarkDirtyAsync(Daily.WithRoot("elsewhere"), "sensor-a", Day, Ct));
    }

    [Fact]
    public async Task Orphaned_temp_files_of_a_file_catalog_are_removed()
    {
        var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"ets-recovery-{Guid.NewGuid():N}"));
        try
        {
            var temp = Path.Combine(dir.FullName, "raw", "2026", "10", "25", "00", ".sensor-a.ets.0123456789abcdef0123456789abcdef.tmp");
            Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
            await File.WriteAllBytesAsync(temp, [1], Ct);
            File.SetLastWriteTimeUtc(temp, Day.AddHours(-1));
            clock.Now = Day.AddHours(1);

            var result = await Maintenance(new FileCatalog(dir)).RecoverAsync(Day, Day.AddDays(1), Ct);

            Assert.Equal(new RecoveryResult(0, 0, 1), result);
            Assert.False(File.Exists(temp));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static Writer Raw(int hour, double value, DateTime? at = null)
        => new Writer(1).AddTimeOrdered([at ?? Day.AddHours(hour)], "t").AddDouble([value], "V");

    private AggregationMaintenance Maintenance(Abstractions.IStorageCatalog catalog, params AggregationSpec[] specs)
        => new(catalog, Configuration(specs));

    private AggregationConfiguration Configuration(params AggregationSpec[] specs) => new()
    {
        Raw = new PathBuilder("raw", TimeGranularity.Hour),
        Specs = specs.Length == 0 ? [Hourly, Daily] : specs,
        RawRetention = TimeSpan.FromDays(30),
        TimeProvider = clock,
        OnFailure = failures.Add,
    };
}
