namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Aggregation.Functions;
using Easy.TimeSeries.Paths;

public class SpecTests
{
    private static AggregationSpec MinuteDaily() => new(AggregationTimeSpan.Minute, TimeGranularity.Day);

    [Theory]
    [InlineData(AggregationTimeSpan.Second, TimeGranularity.Hour, "agg-1s-hourly")]
    [InlineData(AggregationTimeSpan.Minute, TimeGranularity.Day, "agg-1m-daily")]
    [InlineData(AggregationTimeSpan.FifteenMinutes, TimeGranularity.Month, "agg-15m-monthly")]
    [InlineData(AggregationTimeSpan.Day, TimeGranularity.Year, "agg-1d-yearly")]
    [InlineData(AggregationTimeSpan.Hour, TimeGranularity.Hour, "agg-1h-hourly")]
    public void Root_names_both_time_spans(AggregationTimeSpan aggregation, TimeGranularity storage, string root)
    {
        var spec = new AggregationSpec(aggregation, storage);
        Assert.Equal(root, spec.Root);
        Assert.Equal(root, spec.CreatePathBuilder().Root);
        Assert.Equal(storage, spec.CreatePathBuilder().Granularity);
    }

    [Fact]
    public void Aggregation_time_span_must_fit_into_storage_time_span()
    {
        Assert.Throws<ArgumentException>(() => new AggregationSpec(AggregationTimeSpan.Day, TimeGranularity.Hour));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.None));
        Assert.Throws<ArgumentException>(() => MinuteDaily().WithTimeSpans(AggregationTimeSpan.Day, TimeGranularity.Hour));
    }

    [Fact]
    public void Spec_is_immutable()
    {
        var baseSpec = MinuteDaily().Column("Voltage", Agg.Avg);
        var derived = baseSpec.GroupBy("Sensor").WithTimeSpans(AggregationTimeSpan.Hour, TimeGranularity.Month);

        Assert.Null(baseSpec.GroupColumn);
        Assert.Equal("agg-1m-daily", baseSpec.Root);
        Assert.Equal("Sensor", derived.GroupColumn);
        Assert.Equal("agg-1h-monthly", derived.Root);
        Assert.Equal(1, derived.FunctionCount);
    }

    [Fact]
    public void Explicit_root_survives_new_time_spans()
    {
        var spec = MinuteDaily().WithRoot("rollups/minute").WithTimeSpans(AggregationTimeSpan.Hour, TimeGranularity.Month);
        Assert.Equal("rollups/minute", spec.Root);
        Assert.Throws<ArgumentException>(() => MinuteDaily().WithRoot("/abs"));
    }

    [Fact]
    public void Duplicate_labels_fail_immediately()
    {
        Assert.Throws<ArgumentException>(() => MinuteDaily().Column("V", Agg.Q95, Agg.Quantile(0.95)));
        Assert.Throws<ArgumentException>(() => MinuteDaily().Column("V", Agg.Avg).Column("V", Agg.Max));
        Assert.Throws<ArgumentException>(() => MinuteDaily().Column("V", Agg.Avg.As("Count")));
        Assert.Throws<ArgumentException>(() => MinuteDaily().Column("V", Agg.Avg).Function(new Avg<float>("V")));

        // Median and Quantile(0.5) are redundant but carry different labels.
        Assert.Equal(2, MinuteDaily().Column("V", Agg.Median, Agg.Quantile(0.5)).FunctionCount);
    }

    [Fact]
    public void Count_label_can_be_moved_out_of_the_way()
    {
        var spec = MinuteDaily().CountLabel("Rows").Column("V", Agg.Avg.As("Count"));
        Assert.Equal("Rows", spec.CountColumn);
    }

    [Fact]
    public void Labels_longer_than_the_limit_fail()
    {
        var longLabel = new string('x', 118);
        Assert.Throws<ArgumentException>(() => MinuteDaily().Column(longLabel, Agg.StdDev));
        Assert.Equal(1, MinuteDaily().Column(longLabel, Agg.StdDev.As("Spread")).FunctionCount);
    }

    [Fact]
    public void Too_many_columns_fail()
    {
        var spec = MinuteDaily();
        for (var i = 0; i < 253; i++)
        {
            spec = spec.Column($"c{i}", Agg.Avg);
        }

        Assert.Throws<ArgumentException>(() => spec.Column("one-more", Agg.Avg));
    }

    [Fact]
    public void Multi_input_functions_need_a_concrete_output()
    {
        Assert.Throws<ArgumentException>(() => MinuteDaily().Function(new TwoInputs(OutputEncoding.FromInputPrecision)));
        Assert.Throws<ArgumentException>(() => MinuteDaily().Function(new TwoInputs(OutputEncoding.Int32)));
        Assert.Equal(1, MinuteDaily().Function(new TwoInputs(OutputEncoding.Double)).FunctionCount);
    }

    [Fact]
    public void Describe_lists_time_group_count_then_functions()
    {
        var spec = MinuteDaily()
            .GroupBy("Sensor")
            .Column("Voltage", Agg.Avg, Agg.Min, Agg.Max, Agg.Q95, Agg.StdDev.WithOutput(OutputEncoding.ScaledNumber32(3)))
            .Column("Ok")
            .Column("Sensor", Agg.Last.As("LastSensor"))
            .Function(new TwoInputs(OutputEncoding.ScaledNumber64(1)));

        var columns = spec.Describe([Headers.Sensor()]);

        Assert.Equal(
            [
                new(0, "Timestamp", ColumnValueType.DateTimeOrdered, (int)TimePrecision.Seconds, typeof(DateTime)),
                new(1, "Sensor", ColumnValueType.Category, 0, typeof(string)),
                new(2, "Count", ColumnValueType.Int32, 0, typeof(int)),
                new(3, "Voltage.Avg", ColumnValueType.ScaledNumber32, 1, typeof(double)),
                new(4, "Voltage.Min", ColumnValueType.ScaledNumber32, 1, typeof(float)),
                new(5, "Voltage.Max", ColumnValueType.ScaledNumber32, 1, typeof(float)),
                new(6, "Voltage.Q95", ColumnValueType.ScaledNumber32, 1, typeof(double)),
                new(7, "Voltage.StdDev", ColumnValueType.ScaledNumber32, 3, typeof(double)),
                new(8, "Ok.TrueRatio", ColumnValueType.Double, 0, typeof(double)),
                new AggregatedColumn(9, "LastSensor", ColumnValueType.Category, 0, typeof(string)),
                new(10, "Power.Avg", ColumnValueType.ScaledNumber64, 1, typeof(double)),
            ],
            columns);
    }

    [Fact]
    public void Day_aggregation_stores_days_precision()
    {
        var spec = new AggregationSpec(AggregationTimeSpan.Day, TimeGranularity.Month).Column("Current");
        Assert.Equal((int)TimePrecision.Days, spec.Describe([Headers.Sensor()])[0].Meta);
    }

    [Fact]
    public void Time_column_keeps_the_raw_encoding_for_min_max_last_but_unordered()
    {
        var spec = MinuteDaily().Column("Timestamp", Agg.Min, Agg.Max);
        var columns = spec.Describe([Headers.Sensor()]);

        Assert.Equal(ColumnValueType.DateTimeUnordered, columns[2].ValueType);
        Assert.Equal((int)TimePrecision.Milliseconds, columns[2].Meta);
    }

    [Fact]
    public void Finest_encoding_wins_across_raw_files()
    {
        var spec = MinuteDaily().Column("V", Agg.Avg, Agg.Max).Column("C", Agg.Max);
        var columns = spec.Describe(
        [
            Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0), ("V", ColumnValueType.ScaledNumber64, 1), ("C", ColumnValueType.Double, 0)),
            Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0), ("C", ColumnValueType.DoubleRaw, 0), ("V", ColumnValueType.ScaledNumber64, 3)),
        ]);

        Assert.Equal((ColumnValueType.ScaledNumber64, 3), (columns[2].ValueType, columns[2].Meta));
        Assert.Equal((ColumnValueType.ScaledNumber64, 3), (columns[3].ValueType, columns[3].Meta));

        // Double and DoubleRaw decode alike; mixed column types fall back to the lossless type.
        Assert.Equal(ColumnValueType.Double, columns[4].ValueType);
    }

    [Fact]
    public void Scaled_and_lossless_mix_falls_back_to_lossless()
    {
        var spec = MinuteDaily().Column("V", Agg.Avg);
        var columns = spec.Describe(
        [
            Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0), ("V", ColumnValueType.ScaledNumber64, 1)),
            Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0), ("V", ColumnValueType.Double, 0)),
        ]);

        Assert.Equal(ColumnValueType.Double, columns[2].ValueType);
    }

    [Fact]
    public void Decimal_inputs_stay_decimal_except_stddev()
    {
        var spec = MinuteDaily().Column("P", Agg.Avg, Agg.Median, Agg.StdDev);
        var columns = spec.Describe([Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0), ("P", ColumnValueType.Decimal, 0))]);

        Assert.Equal([ColumnValueType.Decimal, ColumnValueType.Decimal, ColumnValueType.Double], columns.Skip(2).Select(x => x.ValueType));
        Assert.Equal([typeof(decimal), typeof(decimal), typeof(double)], columns.Skip(2).Select(x => x.ClrType));
    }

    [Fact]
    public void Missing_or_retyped_inputs_fail_naming_file_and_column()
    {
        var spec = MinuteDaily().Column("V", Agg.Avg);
        var binding = new SpecBinding(spec);
        binding.Bind(Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0), ("V", ColumnValueType.Float, 0)), "a.ets");

        var retyped = Assert.Throws<AggregationException>(
            () => binding.Bind(Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0), ("V", ColumnValueType.Double, 0)), "b.ets"));
        Assert.Equal("b.ets", retyped.RawPath);
        Assert.Equal("V", retyped.Column);

        var missing = Assert.Throws<AggregationException>(
            () => binding.Bind(Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0)), "c.ets"));
        Assert.Contains("c.ets", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Columns_are_matched_by_label_in_each_file()
    {
        var spec = MinuteDaily().Column("V", Agg.Avg);
        var binding = new SpecBinding(spec);
        var a = binding.Bind(Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0), ("V", ColumnValueType.Float, 0)), null);
        var b = binding.Bind(Headers.Of(("V", ColumnValueType.FloatRaw, 0), ("x", ColumnValueType.Int32, 0), ("t", ColumnValueType.DateTimeUnordered, 0)), null);

        Assert.Equal((0, 1), (a.TimeColumn, a.SlotColumns[0]));
        Assert.Equal((2, 0), (b.TimeColumn, b.SlotColumns[0]));
        Assert.Equal([0, 2], b.ReadColumns);
    }

    [Fact]
    public void Time_column_must_be_named_when_ambiguous()
    {
        var header = Headers.Of(("a", ColumnValueType.DateTimeOrdered, 0), ("b", ColumnValueType.DateTimeUnordered, 0), ("V", ColumnValueType.Float, 0));

        Assert.Throws<AggregationException>(() => MinuteDaily().Column("V").Describe([header]));
        Assert.Equal("b", MinuteDaily().Time("b").Column("V").Describe([header])[0].Label);
        Assert.Throws<AggregationException>(() => MinuteDaily().Time("V").Column("V").Describe([header]));
    }

    [Fact]
    public void Group_column_must_be_a_category()
    {
        Assert.Throws<AggregationException>(() => MinuteDaily().GroupBy("Current").Column("Voltage").Describe([Headers.Sensor()]));
    }

    [Fact]
    public void Index_inputs_must_point_to_the_same_label_everywhere()
    {
        var spec = MinuteDaily().Function(new Avg<float>(1, label: "Second"));
        var binding = new SpecBinding(spec);
        binding.Bind(Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0), ("V", ColumnValueType.Float, 0)), null);

        Assert.Throws<AggregationException>(
            () => binding.Bind(Headers.Of(("t", ColumnValueType.DateTimeOrdered, 0), ("W", ColumnValueType.Float, 0)), null));
    }

    [Fact]
    public void Custom_input_with_a_wrong_type_fails()
    {
        var spec = MinuteDaily().Function(new Avg<double>("Voltage"));
        Assert.Throws<AggregationException>(() => spec.Describe([Headers.Sensor()]));
    }

    [Fact]
    public void Time_label_colliding_with_a_function_label_fails_at_bind()
    {
        var spec = MinuteDaily().Column("Current", Agg.Avg.As("Timestamp"));
        Assert.Throws<AggregationException>(() => spec.Describe([Headers.Sensor()]));
    }

    [Fact]
    public void Incompatible_explicit_output_fails_at_bind()
    {
        var spec = MinuteDaily().Column("Current", Agg.Avg.WithOutput(OutputEncoding.Int32));
        var ex = Assert.Throws<AggregationException>(() => spec.Describe([Headers.Sensor()]));
        Assert.Equal("Current.Avg", ex.Function);
    }

    private sealed class TwoInputs(OutputEncoding output) : IAggregationFunction<double>
    {
        public string Name => "Power.Avg";

        public IReadOnlyList<AggregationInput> Inputs { get; } =
            [AggregationInput.Of<float>("Voltage"), AggregationInput.Of<double>("Current")];

        public OutputEncoding Output => output;

        public double Compute(in AggregationSlice slice) => 0;
    }
}
