namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Aggregation.Functions;

public class FunctionTests
{
    [Fact]
    public void Avg_of_integers_is_exact_beyond_double_precision()
    {
        long[] values = [long.MaxValue, long.MaxValue, -long.MaxValue];
        Assert.Equal(long.MaxValue / 3d, Slices.Compute(new Avg<long>("x"), values));
        Assert.Equal(2d, Slices.Compute(new Avg<int>("x"), new[] { 1, 2, 3 }));
    }

    [Fact]
    public void Avg_of_doubles_is_compensated()
    {
        // Naive summation loses the small values entirely next to 1e16.
        double[] values = [1e16, 1d, 1d, 1d, 1d, -1e16];
        Assert.Equal(4d / 6, Slices.Compute(new Avg<double>("x"), values), 15);
    }

    [Fact]
    public void Avg_skips_nan_and_propagates_infinity()
    {
        Assert.Equal(2d, Slices.Compute(new Avg<double>("x"), new[] { 1d, double.NaN, 3d }));
        Assert.Equal(2d, Slices.Compute(new Avg<float>("x"), new[] { 1f, float.NaN, 3f }));
        Assert.True(double.IsNaN(Slices.Compute(new Avg<double>("x"), new[] { double.NaN })));
        Assert.Equal(double.PositiveInfinity, Slices.Compute(new Avg<double>("x"), new[] { 1d, double.PositiveInfinity }));
        Assert.True(double.IsNaN(Slices.Compute(new Avg<double>("x"), new[] { double.NegativeInfinity, double.PositiveInfinity })));
    }

    [Fact]
    public void Avg_of_decimal_is_exact()
    {
        Assert.Equal(0.2m, Slices.Compute(new AvgDecimal("x"), new[] { 0.1m, 0.2m, 0.3m }));
    }

    [Fact]
    public void Generic_avg_and_quantile_reject_decimal()
    {
        var avg = Assert.Throws<ArgumentException>(() => new Avg<decimal>("x"));
        Assert.Contains(nameof(AvgDecimal), avg.Message, StringComparison.Ordinal);
        var quantile = Assert.Throws<ArgumentException>(() => new Quantile<decimal>("x", 0.5));
        Assert.Contains(nameof(QuantileDecimal), quantile.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0d, 1d)]
    [InlineData(0.25, 1.75)]
    [InlineData(0.5, 2.5)]
    [InlineData(0.9, 3.7)]
    [InlineData(1d, 4d)]
    public void Quantile_interpolates_linearly_like_numpy(double p, double expected)
    {
        // numpy.quantile([4, 1, 3, 2], p) with the default method "linear" (Hyndman-Fan type 7).
        Assert.Equal(expected, Slices.Compute(new Quantile<int>("x", p), new[] { 4, 1, 3, 2 }), 12);
        Assert.Equal((decimal)expected, Slices.Compute(new QuantileDecimal("x", p), new[] { 4m, 1m, 3m, 2m }));
    }

    [Fact]
    public void Quantile_skips_nan_and_handles_infinity()
    {
        Assert.Equal(2d, Slices.Compute(new Quantile<double>("x", 0.5), new[] { double.NaN, 1d, 3d }));
        Assert.True(double.IsNaN(Slices.Compute(new Quantile<double>("x", 0.5), new[] { double.NaN })));
        Assert.Equal(
            double.PositiveInfinity,
            Slices.Compute(new Quantile<double>("x", 0.99), new[] { 1d, double.PositiveInfinity, double.PositiveInfinity }));
    }

    [Theory]
    [InlineData(0.5, "x.Q50")]
    [InlineData(0.07, "x.Q7")]
    [InlineData(0.9, "x.Q90")]
    [InlineData(0.999, "x.Q99.9")]
    [InlineData(1d, "x.Q100")]
    public void Quantile_labels_have_no_floating_point_noise(double p, string label)
    {
        Assert.Equal(label, new Quantile<double>("x", p).Name);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Quantile_level_must_lie_in_unit_interval(double p)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantile<double>("x", p));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantile<double>("x", p, label: "Q"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantile<double>(0, p, label: "Q"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuantileDecimal("x", p, label: "Q"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Agg.Quantile(p));
    }

    [Fact]
    public void StdDev_is_the_population_deviation()
    {
        Assert.Equal(2d, Slices.Compute(new StdDev<int>("x"), new[] { 2, 4, 4, 4, 5, 5, 7, 9 }), 12);
        Assert.Equal(0d, Slices.Compute(new StdDev<double>("x"), new[] { 42d }));
        Assert.Equal(2d, Slices.Compute(new StdDev<decimal>("x"), new[] { 2m, 4m, 4m, 4m, 5m, 5m, 7m, 9m }), 12);
    }

    [Fact]
    public void StdDev_stays_accurate_with_a_large_offset()
    {
        double[] values = [1e9 + 4, 1e9 + 7, 1e9 + 13, 1e9 + 16];
        Assert.Equal(Math.Sqrt(22.5), Slices.Compute(new StdDev<double>("x"), values), 9);
    }

    [Fact]
    public void StdDev_keeps_the_spread_of_large_int64_values()
    {
        Assert.Equal(0.5d, Slices.Compute(new StdDev<long>("x"), new[] { 9007199254740992L, 9007199254740993L }));
        Assert.Equal(50d, Slices.Compute(new StdDev<long>("x"), new[] { 1_800_000_000_000_000_000L, 1_800_000_000_000_000_100L }));
        Assert.Equal(
            long.MaxValue / 2d,
            Slices.Compute(new StdDev<long>("x"), new[] { long.MaxValue, 0L }),
            1e-12 * long.MaxValue);
    }

    [Fact]
    public void Quantile_between_opposite_sign_extremes_does_not_overflow()
    {
        Assert.Equal(0d, Slices.Compute(new Quantile<double>("x", 0.5), new[] { -1e308, 1e308 }));
        Assert.Equal(5e307, Slices.Compute(new Quantile<double>("x", 0.75), new[] { -1e308, 1e308 }), 1e293);
        Assert.Equal(0m, Slices.Compute(new QuantileDecimal("x", 0.5), new[] { decimal.MinValue, decimal.MaxValue }));
    }

    [Fact]
    public void StdDev_skips_nan_and_returns_infinity_for_infinite_values()
    {
        Assert.Equal(1d, Slices.Compute(new StdDev<double>("x"), new[] { 1d, double.NaN, 3d }));
        Assert.Equal(double.PositiveInfinity, Slices.Compute(new StdDev<double>("x"), new[] { 1d, double.NegativeInfinity }));
    }

    [Fact]
    public void Min_and_max_skip_nan()
    {
        double[] values = [double.NaN, 3d, -1d, double.NaN, 2d];
        Assert.Equal(-1d, Slices.Compute(new Min<double>("x"), values));
        Assert.Equal(3d, Slices.Compute(new Max<double>("x"), values));
        Assert.True(float.IsNaN(Slices.Compute(new Min<float>("x"), new[] { float.NaN, float.NaN })));
    }

    [Fact]
    public void Min_and_max_cover_time_types()
    {
        var a = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime[] times = [a.AddHours(1), a, a.AddHours(2)];
        Assert.Equal(a, Slices.Compute(new Min<DateTime>("t"), times));
        Assert.Equal(a.AddHours(2), Slices.Compute(new Max<DateTime>("t"), times));
        Assert.Equal(TimeSpan.FromSeconds(9), Slices.Compute(new Max<TimeSpan>("d"), new[] { TimeSpan.FromSeconds(9), TimeSpan.Zero }));
    }

    [Fact]
    public void Last_returns_the_last_row_as_is()
    {
        Assert.Equal("b", Slices.Compute(new Last<string>("s"), new[] { "a", "b" }));
        Assert.True(double.IsNaN(Slices.Compute(new Last<double>("x"), new[] { 1d, double.NaN })));
    }

    [Fact]
    public void True_ratio_counts_true_values()
    {
        Assert.Equal(0.75, Slices.Compute(new TrueRatio("ok"), new[] { true, false, true, true }));
    }

    [Fact]
    public void Functions_name_column_index_inputs()
    {
        var avg = new Avg<double>(3);
        Assert.Equal("#3.Avg", avg.Name);
        Assert.Equal(3, avg.Inputs[0].ColumnIndex);
    }

    [Fact]
    public void Default_outputs_follow_the_result_encoding_rules()
    {
        Assert.Equal(OutputEncoding.FromInputPrecision, new Avg<float>("x").Output);
        Assert.Equal(OutputEncoding.FromInputPrecision, new StdDev<double>("x").Output);
        Assert.Equal(OutputEncoding.SameAsInput, new Min<int>("x").Output);
        Assert.Equal(OutputEncoding.SameAsInput, new Last<string>("x").Output);
        Assert.Equal(OutputEncoding.Double, new Avg<float>("x", output: OutputEncoding.Double).Output);
        Assert.Equal(typeof(double), ((IAggregationFunction)new Avg<int>("x")).ResultType);
        Assert.Equal(typeof(int), ((IAggregationFunction)new Max<int>("x")).ResultType);
    }
}
