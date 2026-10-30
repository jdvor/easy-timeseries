namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;

/// <summary>
/// Compares the downsampler with a straightforward LINQ implementation over randomized raw data: unordered and equal
/// timestamps, gaps, grouping, NaN, several raw files per chunk, and schema drift between raw files.
/// </summary>
public class OracleTests
{
    private static readonly DateTime Day = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string[] Groups = ["sensor-b", "a", "Z", "sensor-a"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AggregationSpec FullSpec(AggregationTimeSpan aggregation, TimeGranularity storage)
        => new AggregationSpec(aggregation, storage)
            .Time("Timestamp")
            .GroupBy("Sensor")
            .Column("Value", Agg.Avg, Agg.Min, Agg.Max, Agg.Median, Agg.Q95, Agg.StdDev, Agg.Last)
            .Column("Ok")
            .Column("Status", Agg.Last)
            .Column("Timestamp", Agg.Min, Agg.Max);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Ten_seconds_hourly_over_one_raw_file(int seed)
    {
        var start = Day.AddHours(14);
        var file = RawData.Generate(new Random(seed), start, TimeSpan.FromHours(1), rows: 600);

        await AssertMatchesOracleAsync(FullSpec(AggregationTimeSpan.TenSeconds, TimeGranularity.Hour), TimeGranularity.Hour, start, [file]);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Minute_daily_over_hourly_raw_files(int seed)
    {
        var random = new Random(seed);
        var files = Enumerable.Range(0, 24)
            .Where(h => h % 5 != 3)
            .Select(h => RawData.Generate(random, Day.AddHours(h), TimeSpan.FromHours(1), rows: random.Next(1, 200)))
            .Reverse()
            .ToArray();

        await AssertMatchesOracleAsync(FullSpec(AggregationTimeSpan.Minute, TimeGranularity.Day), TimeGranularity.Hour, Day, files);
    }

    [Fact]
    public async Task Day_aggregation_over_hourly_raw_files_spans_several_files_per_slice()
    {
        var random = new Random(6);
        var month = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var files = Enumerable.Range(0, 72)
            .Select(h => RawData.Generate(random, month.AddHours(h), TimeSpan.FromHours(1), rows: random.Next(1, 40)))
            .ToArray();

        await AssertMatchesOracleAsync(FullSpec(AggregationTimeSpan.Day, TimeGranularity.Month), TimeGranularity.Hour, month, files);
    }

    [Fact]
    public async Task Fifteen_minutes_monthly_over_daily_raw_files()
    {
        var random = new Random(7);
        var month = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var files = Enumerable.Range(0, 28)
            .Select(d => RawData.Generate(random, month.AddDays(d), TimeSpan.FromDays(1), rows: random.Next(1, 300)))
            .ToArray();

        await AssertMatchesOracleAsync(FullSpec(AggregationTimeSpan.FifteenMinutes, TimeGranularity.Month), TimeGranularity.Day, month, files);
    }

    [Fact]
    public async Task Ungrouped_ordered_raw_file_uses_rows_in_place()
    {
        var random = new Random(8);
        var start = Day.AddHours(3);
        var file = RawData.Generate(random, start, TimeSpan.FromHours(1), rows: 500, ordered: true, groups: 1);
        var spec = new AggregationSpec(AggregationTimeSpan.FiveMinutes, TimeGranularity.Day)
            .Column("Value", Agg.Avg, Agg.Q99, Agg.Last);

        await AssertMatchesOracleAsync(spec, TimeGranularity.Hour, Day, [file]);
    }

    [Fact]
    public async Task Schema_drift_between_raw_files_is_bound_per_file()
    {
        var random = new Random(9);
        var first = RawData.Generate(random, Day.AddHours(1), TimeSpan.FromHours(1), rows: 100);
        var second = RawData.Generate(random, Day.AddHours(2), TimeSpan.FromHours(1), rows: 100) with { Drifted = true };

        await AssertMatchesOracleAsync(FullSpec(AggregationTimeSpan.Minute, TimeGranularity.Day), TimeGranularity.Hour, Day, [first, second]);
    }

    [Fact]
    public async Task Custom_function_reads_two_columns_of_different_types()
    {
        var random = new Random(10);
        var file = RawData.Generate(random, Day.AddHours(14), TimeSpan.FromHours(1), rows: 300);
        var spec = new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Day)
            .GroupBy("Sensor")
            .Function(new AveragePower(voltage: "Voltage", current: "Value"));

        var output = await RunAsync(spec, TimeGranularity.Hour, Day, [file]);

        var expected = Oracle.Slices(spec, [file]).ToArray();
        Assert.Equal(expected.Length, output.Rows);
        for (var i = 0; i < expected.Length; i++)
        {
            var rows = expected[i].Rows.Where(r => !double.IsNaN(r.Value)).ToArray();
            var power = rows.Length == 0 ? double.NaN : rows.Average(r => r.Voltage * r.Value);
            AssertClose(Math.Round(power, 1), (double)output["Power.Avg"][i], $"row {i}");
        }
    }

    private static async Task AssertMatchesOracleAsync(
        AggregationSpec spec,
        TimeGranularity raw,
        DateTime spanStart,
        IReadOnlyList<RawData> files)
    {
        var output = await RunAsync(spec, raw, spanStart, files);
        var expected = Oracle.Slices(spec, files).ToArray();

        Assert.Equal(expected.Length, output.Rows);
        var hasGroup = spec.GroupColumn is not null;
        for (var i = 0; i < expected.Length; i++)
        {
            var slice = expected[i];
            var at = $"row {i} ({slice.Start:o} {slice.Group})";
            Assert.Equal(slice.Start, output["Timestamp"][i]);
            if (hasGroup)
            {
                Assert.Equal(slice.Group, output["Sensor"][i]);
            }

            Assert.Equal(slice.Rows.Length, output["Count"][i]);
            foreach (var (label, value) in Oracle.Expected(spec, slice))
            {
                var actual = output[label][i];
                if (value is double d)
                {
                    AssertClose(d, (double)actual, $"{label} at {at}");
                }
                else
                {
                    Assert.True(Equals(value, actual), $"{label} at {at}: expected {value}, got {actual}");
                }
            }
        }
    }

    private static async Task<Columns> RunAsync(
        AggregationSpec spec,
        TimeGranularity raw,
        DateTime spanStart,
        IReadOnlyList<RawData> files)
    {
        var rawFiles = new List<RawFile>();
        foreach (var file in files)
        {
            var bytes = await Columns.WriteAsync(file.ToWriter(), Ct);
            rawFiles.Add(new RawFile(file.BucketStart, new MemoryStorage(bytes), file.BucketStart.ToString("o")));
        }

        using var target = new InMemoryStorage();
        var result = await new Downsampler(spec, raw).RunAsync(spanStart, rawFiles, target, Ct);

        Assert.Equal(files.Sum(x => x.Rows.Length), result.RawRows);
        return Columns.Read(target.WrittenMemory);
    }

    private static void AssertClose(double expected, double actual, string what)
    {
        if (double.IsNaN(expected) || double.IsInfinity(expected))
        {
            Assert.True(expected.Equals(actual), $"{what}: expected {expected}, got {actual}");
            return;
        }

        var tolerance = 1e-9 * Math.Max(1, Math.Abs(expected));
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"{what}: expected {expected}, got {actual}");
    }

    private sealed class MemoryStorage(byte[] bytes) : Abstractions.IReadStorage
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken) => Task.FromResult<ReadOnlyMemory<byte>>(bytes);
    }

    /// <summary>The example from the aggregation docs: avg(voltage x current) per row.</summary>
    private sealed class AveragePower(string voltage, string current) : IAggregationFunction<double>
    {
        public string Name => "Power.Avg";

        public IReadOnlyList<AggregationInput> Inputs { get; } =
        [
            AggregationInput.Of<float>(voltage),
            AggregationInput.Of<double>(current),
        ];

        public OutputEncoding Output => OutputEncoding.ScaledNumber64(decimalPlaces: 1);

        public double Compute(in AggregationSlice slice)
        {
            var u = slice.Values<float>(0);
            var i = slice.Values<double>(1);
            var sum = 0d;
            var n = 0;
            for (var k = 0; k < u.Length; k++)
            {
                if (!double.IsNaN(i[k]))
                {
                    sum += u[k] * i[k];
                    ++n;
                }
            }

            return n == 0 ? double.NaN : sum / n;
        }
    }

    internal sealed record Row(DateTime Time, string Sensor, double Value, bool Ok, string Status, float Voltage, int FileOrder, int Index);

    internal sealed record RawData(DateTime BucketStart, Row[] Rows, bool Drifted = false)
    {
        public static RawData Generate(Random random, DateTime start, TimeSpan length, int rows, bool ordered = false, int groups = 4)
        {
            // A gap: nothing is generated in the second fifth of the span.
            var ms = (long)length.TotalMilliseconds;
            var times = new DateTime[rows];
            for (var i = 0; i < rows; i++)
            {
                var offset = random.NextInt64(ms);
                if (offset > ms / 5 && offset < 2 * ms / 5)
                {
                    offset -= ms / 5;
                }

                times[i] = i > 0 && random.Next(10) == 0 ? times[i - 1] : start.AddMilliseconds(offset);
            }

            if (ordered)
            {
                Array.Sort(times);
            }

            var result = new Row[rows];
            for (var i = 0; i < rows; i++)
            {
                result[i] = new Row(
                    times[i],
                    Groups[random.Next(groups)],
                    random.Next(50) == 0 ? double.NaN : Math.Round((random.NextDouble() * 1000) - 300, 3),
                    random.Next(3) > 0,
                    random.Next(4) == 0 ? "warn" : "ok",
                    (float)Math.Round(220 + (random.NextDouble() * 20), 1),
                    0,
                    i);
            }

            return new RawData(start, result);
        }

        /// <summary>Raw file; the drifted variant reorders columns and changes encodings that decode alike.</summary>
        public Writer ToWriter()
        {
            var writer = new Writer(Rows.Length);
            if (Drifted)
            {
                return writer
                    .AddDoubleRandom(Rows.Select(x => x.Value), "Value")
                    .AddCategory(Rows.Select(x => x.Status), "Status")
                    .AddInt32(Rows.Select(x => x.Index), "Extra")
                    .AddTimeUnordered(Rows.Select(x => x.Time), "Timestamp")
                    .AddBool(Rows.Select(x => x.Ok), "Ok")
                    .AddCategory(Rows.Select(x => x.Sensor), "Sensor")
                    .AddFloatRandom(Rows.Select(x => x.Voltage), "Voltage");
            }

            return writer
                .AddTimeUnordered(Rows.Select(x => x.Time), "Timestamp")
                .AddCategory(Rows.Select(x => x.Sensor), "Sensor")
                .AddDouble(Rows.Select(x => x.Value), "Value")
                .AddBool(Rows.Select(x => x.Ok), "Ok")
                .AddCategory(Rows.Select(x => x.Status), "Status")
                .AddScaledNumber32(Rows.Select(x => x.Voltage), "Voltage", 1);
        }
    }

    internal sealed record Slice(DateTime Start, string? Group, Row[] Rows);

    private static class Oracle
    {
        public static IEnumerable<Slice> Slices(AggregationSpec spec, IReadOnlyList<RawData> files)
        {
            var span = spec.AggregationTimeSpan.ToTimeSpan().Ticks;
            var grouped = spec.GroupColumn is not null;
            return files
                .OrderBy(f => f.BucketStart)
                .SelectMany((f, fileOrder) => f.Rows.Select(r => r with { FileOrder = fileOrder }))
                .GroupBy(r => (Start: new DateTime(r.Time.Ticks - (r.Time.Ticks % span), DateTimeKind.Utc), Group: grouped ? r.Sensor : null))
                .OrderBy(g => g.Key.Start)
                .ThenBy(g => g.Key.Group, StringComparer.Ordinal)
                .Select(g => new Slice(
                    g.Key.Start,
                    g.Key.Group,
                    [.. g.OrderBy(r => r.Time).ThenBy(r => r.FileOrder).ThenBy(r => r.Index)]));
        }

        public static IEnumerable<(string Label, object Value)> Expected(AggregationSpec spec, Slice slice)
        {
            var values = slice.Rows.Select(r => r.Value).Where(v => !double.IsNaN(v)).Order().ToArray();
            foreach (var entry in spec.Entries.OfType<ColumnEntry>())
            {
                foreach (var descriptor in entry.Descriptors)
                {
                    var label = descriptor.LabelFor(entry.Label) ?? (entry.Label == "Ok" ? "Ok.TrueRatio" : $"{entry.Label}.Avg");
                    var suffix = label[(entry.Label.Length + 1)..];
                    yield return (label, (entry.Label, suffix) switch
                    {
                        ("Value", "Avg") => values.Length == 0 ? double.NaN : values.Average(),
                        ("Value", "Min") => values.Length == 0 ? double.NaN : values[0],
                        ("Value", "Max") => values.Length == 0 ? double.NaN : values[^1],
                        ("Value", "Median") => Quantile(values, 0.5),
                        ("Value", "Q95") => Quantile(values, 0.95),
                        ("Value", "Q99") => Quantile(values, 0.99),
                        ("Value", "StdDev") => StdDev(values),
                        ("Value", "Last") => slice.Rows[^1].Value,
                        ("Ok", "TrueRatio") => slice.Rows.Count(r => r.Ok) / (double)slice.Rows.Length,
                        ("Status", "Last") => slice.Rows[^1].Status,
                        ("Timestamp", "Min") => slice.Rows[0].Time,
                        ("Timestamp", "Max") => slice.Rows[^1].Time,
                        _ => throw new InvalidOperationException($"no oracle for {label}"),
                    });
                }
            }
        }

        private static double Quantile(double[] sorted, double p)
        {
            if (sorted.Length == 0)
            {
                return double.NaN;
            }

            var h = (sorted.Length - 1) * p;
            var lo = (int)Math.Floor(h);
            var hi = Math.Min(lo + 1, sorted.Length - 1);
            return sorted[lo] + ((h - lo) * (sorted[hi] - sorted[lo]));
        }

        private static double StdDev(double[] values)
        {
            if (values.Length == 0)
            {
                return double.NaN;
            }

            var mean = values.Average();
            return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Length);
        }
    }
}
