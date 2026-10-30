namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;

public class DownsamplerTests
{
    private static readonly DateTime Hour14 = new(2026, 10, 25, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hourly_raw_file_aggregates_per_minute_and_group()
    {
        DateTime[] t = [Hour14.AddSeconds(5), Hour14.AddSeconds(10), Hour14.AddSeconds(20), Hour14.AddSeconds(65), Hour14.AddSeconds(70)];
        string[] sensor = ["b", "a", "b", "a", "a"];
        float[] voltage = [230.1f, 229.5f, 231.3f, 230f, 232f];
        var raw = await Columns.WriteAsync(
            new Writer(t.Length)
                .AddTimeOrdered(t, "Timestamp")
                .AddCategory(sensor, "Sensor")
                .AddScaledNumber32(voltage, "Voltage", 1),
            Ct);

        var spec = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour)
            .GroupBy("Sensor")
            .Column("Voltage", Agg.Avg, Agg.Max);
        using var target = new InMemoryStorage();

        var result = await new Downsampler(spec, TimeGranularity.Hour).RunAsync(Hour14, new RawBuffer(Hour14, raw), target, Ct);

        Assert.Equal(new AggregationResult(Rows: 3, Groups: 2, RawFiles: 1, RawRows: 5), result);
        var output = Columns.Read(target.WrittenMemory);
        Assert.Equal([Hour14, Hour14, Hour14.AddMinutes(1)], output["Timestamp"]);
        Assert.Equal(["a", "b", "a"], output["Sensor"]);
        Assert.Equal([1, 2, 2], output["Count"]);
        Assert.Equal([229.5f, 230.7f, 231f], output["Voltage.Avg"]);
        Assert.Equal([229.5f, 231.3f, 232f], output["Voltage.Max"]);
        Assert.Equal(ColumnValueType.ScaledNumber32, output.Header.Columns[3].ValueType);
    }

    [Fact]
    public async Task No_rows_write_nothing()
    {
        var spec = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Day).Column("V");
        var target = new RecordingStorage();

        var result = await new Downsampler(spec, TimeGranularity.Hour).RunAsync(Day, [], target, Ct);

        Assert.False(result.Written);
        Assert.Equal(0, target.Writes);
        Assert.False(target.Closed);
    }

    [Fact]
    public async Task Catalog_run_reads_the_subjects_raw_files_and_writes_the_aggregated_path()
    {
        var catalog = new InMemoryCatalog();
        var raw = new PathBuilder("raw", TimeGranularity.Hour);
        foreach (var hour in new[] { 0, 5, 23 })
        {
            var at = Day.AddHours(hour);
            await WriteRawAsync(catalog, raw.GetFilePath(at, "sensor-a"), [at.AddMinutes(1), at.AddMinutes(2)], [hour, hour + 1d]);
        }

        // Another subject and the next day must not be read.
        await WriteRawAsync(catalog, raw.GetFilePath(Day.AddHours(1), "sensor-b"), [Day.AddHours(1)], [100d]);
        await WriteRawAsync(catalog, raw.GetFilePath(Day.AddDays(1), "sensor-a"), [Day.AddDays(1)], [100d]);

        var spec = new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).Column("V", Agg.Avg);
        var result = await new Downsampler(spec, TimeGranularity.Hour).RunAsync(catalog, raw, "sensor-a", Day, Ct);

        Assert.Equal(new AggregationResult(3, 0, 3, 6), result);
        var output = await Columns.ReadAsync(catalog.OpenRead("agg-1h-daily/2026/10/25/sensor-a.ets"), Ct);
        Assert.Equal([Day, Day.AddHours(5), Day.AddHours(23)], output["t"]);
        Assert.Equal([0.5, 5.5, 23.5], output["V.Avg"]);
    }

    [Fact]
    public async Task Catalog_run_without_raw_files_writes_nothing()
    {
        var catalog = new InMemoryCatalog();
        var spec = new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).Column("V");

        var result = await new Downsampler(spec, TimeGranularity.Hour)
            .RunAsync(catalog, new PathBuilder("raw", TimeGranularity.Hour), "sensor-a", Day, Ct);

        Assert.False(result.Written);
        Assert.Equal(0, catalog.Count);
    }

    [Fact]
    public void Aggregated_storage_time_span_must_not_be_shorter_than_raw()
    {
        var spec = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour);
        Assert.Throws<ArgumentException>(() => new Downsampler(spec, TimeGranularity.Day));
    }

    [Fact]
    public async Task Span_start_and_raw_buckets_are_validated()
    {
        var spec = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Day).Column("V");
        var downsampler = new Downsampler(spec, TimeGranularity.Hour);
        using var target = new InMemoryStorage();
        var storage = new InMemoryStorage();

        await Assert.ThrowsAsync<ArgumentException>(() => downsampler.RunAsync(Day.AddHours(1), [], target, Ct));
        await Assert.ThrowsAsync<ArgumentException>(
            () => downsampler.RunAsync(Day, [new RawFile(Day.AddDays(1), storage)], target, Ct));
        await Assert.ThrowsAsync<ArgumentException>(
            () => downsampler.RunAsync(Day, [new RawFile(Day.AddMinutes(30), storage)], target, Ct));
    }

    [Fact]
    public async Task Row_outside_its_raw_bucket_fails_naming_the_file()
    {
        var raw = await Columns.WriteAsync(
            new Writer(2).AddTimeOrdered([Hour14, Hour14.AddHours(1)], "t").AddDouble([1d, 2d], "V"),
            Ct);
        var spec = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Day).Column("V");
        using var target = new InMemoryStorage();

        var ex = await Assert.ThrowsAsync<AggregationException>(
            () => new Downsampler(spec, TimeGranularity.Hour).RunAsync(Day, new RawBuffer(Hour14, raw, "late.ets"), target, Ct));

        Assert.Equal("late.ets", ex.RawPath);
        Assert.Equal(0, target.Size);
    }

    [Fact]
    public async Task Scaled_result_out_of_range_fails_naming_the_function()
    {
        var raw = await Columns.WriteAsync(
            new Writer(1).AddTimeOrdered([Hour14], "t").AddDouble([1e12], "V"),
            Ct);
        var spec = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour)
            .Column("V", Agg.Avg.WithOutput(OutputEncoding.ScaledNumber32(2)));
        using var target = new InMemoryStorage();

        var ex = await Assert.ThrowsAsync<AggregationException>(
            () => new Downsampler(spec, TimeGranularity.Hour).RunAsync(Hour14, new RawBuffer(Hour14, raw), target, Ct));

        Assert.Equal("V.Avg", ex.Function);
    }

    [Theory]
    [InlineData(-21474836.48, true)]
    [InlineData(-21474836.49, false)]
    [InlineData(21474836.47, true)]
    [InlineData(21474836.48, false)]
    public async Task Scaled_result_range_includes_the_integer_minimum(double value, bool fits)
    {
        var raw = await Columns.WriteAsync(
            new Writer(1).AddTimeOrdered([Hour14], "t").AddDouble([value], "V"),
            Ct);
        var spec = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour)
            .Column("V", Agg.Avg.WithOutput(OutputEncoding.ScaledNumber32(2)));
        using var target = new InMemoryStorage();
        var run = () => new Downsampler(spec, TimeGranularity.Hour).RunAsync(Hour14, new RawBuffer(Hour14, raw), target, Ct);

        if (fits)
        {
            Assert.Equal(1, (await run()).Rows);
        }
        else
        {
            await Assert.ThrowsAsync<AggregationException>(run);
        }
    }

    [Fact]
    public async Task Nan_result_for_a_scaled_encoding_fails()
    {
        var raw = await Columns.WriteAsync(
            new Writer(1).AddTimeOrdered([Hour14], "t").AddScaledNumber64([double.NaN], "V", 2),
            Ct);
        var spec = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour).Column("V", Agg.StdDev);
        using var target = new InMemoryStorage();

        // The scaled writer stores NaN as 0, so the raw value is 0 and StdDev is 0; NaN arises only from all-NaN input.
        var result = await new Downsampler(spec, TimeGranularity.Hour).RunAsync(Hour14, new RawBuffer(Hour14, raw), target, Ct);
        Assert.True(result.Written);

        var doubles = await Columns.WriteAsync(
            new Writer(1).AddTimeOrdered([Hour14], "t").AddDouble([double.NaN], "V"),
            Ct);
        var toScaled = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour)
            .Column("V", Agg.Avg.WithOutput(OutputEncoding.ScaledNumber64(2)));
        using var scaledTarget = new InMemoryStorage();
        await Assert.ThrowsAsync<AggregationException>(
            () => new Downsampler(toScaled, TimeGranularity.Hour).RunAsync(Hour14, new RawBuffer(Hour14, doubles), scaledTarget, Ct));
    }

    [Fact]
    public async Task More_groups_than_a_category_column_holds_fail()
    {
        // One raw Category column holds at most 64 KiB of labels, so the limit is reached across three files.
        const int perFile = 11_000;
        var files = new List<RawFile>();
        for (var hour = 0; hour < 3; hour++)
        {
            var at = Day.AddHours(hour);
            var raw = await Columns.WriteAsync(
                new Writer(perFile)
                    .AddTimeOrdered(Enumerable.Repeat(at, perFile), "t")
                    .AddCategory(Enumerable.Range(hour * perFile, perFile).Select(ShortLabel), "Sensor")
                    .AddDouble(Enumerable.Repeat(1d, perFile), "V"),
                Ct);
            files.Add(new RawFile(at, new InMemoryReadStorage(raw)));
        }

        var spec = new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).GroupBy("Sensor").Column("V");
        using var target = new InMemoryStorage();

        var ex = await Assert.ThrowsAsync<AggregationException>(
            () => new Downsampler(spec, TimeGranularity.Hour).RunAsync(Day, files, target, Ct));

        Assert.Equal("Sensor", ex.Column);
        Assert.Contains("32767", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Group_labels_over_the_category_size_limit_fail_naming_the_column()
    {
        // 20 files x 1,000 groups of 8 characters: 20,000 groups fit the count limit but not the 64 KiB label limit.
        var files = new List<RawFile>();
        for (var hour = 0; hour < 20; hour++)
        {
            var at = Day.AddHours(hour);
            var raw = await Columns.WriteAsync(
                new Writer(1000)
                    .AddTimeOrdered(Enumerable.Repeat(at, 1000), "t")
                    .AddCategory(Enumerable.Range(hour * 1000, 1000).Select(i => $"g{i:D7}"), "Sensor")
                    .AddDouble(Enumerable.Repeat(1d, 1000), "V"),
                Ct);
            files.Add(new RawFile(at, new InMemoryReadStorage(raw)));
        }

        var spec = new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).GroupBy("Sensor").Column("V");
        using var target = new InMemoryStorage();

        var ex = await Assert.ThrowsAsync<AggregationException>(
            () => new Downsampler(spec, TimeGranularity.Hour).RunAsync(Day, files, target, Ct));

        Assert.Equal("Sensor", ex.Column);
        Assert.Equal(0, target.Size);
    }

    /// <summary>Distinct three-character labels, so many fit into one raw Category column.</summary>
    private static string ShortLabel(int i)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return $"{alphabet[i / (62 * 62)]}{alphabet[i / 62 % 62]}{alphabet[i % 62]}";
    }

    private static async Task WriteRawAsync(InMemoryCatalog catalog, string path, DateTime[] times, double[] values)
    {
        await using var storage = catalog.OpenWrite(path);
        using var writer = new Writer(times.Length).AddTimeOrdered(times, "t").AddDouble(values, "V");
        await writer.WriteToAsync(storage, Ct);
    }

    private sealed class InMemoryReadStorage(byte[] bytes) : Abstractions.IReadStorage
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken) => Task.FromResult<ReadOnlyMemory<byte>>(bytes);
    }

    private sealed class RecordingStorage : Abstractions.IWriteStorage
    {
        public int Writes { get; private set; }

        public bool Closed { get; private set; }

        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            ++Writes;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            Closed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
