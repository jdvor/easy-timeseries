namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Aggregation.Functions;

public class DescriptorTests
{
    [Theory]
    [InlineData(typeof(float), typeof(Avg<float>))]
    [InlineData(typeof(double), typeof(Avg<double>))]
    [InlineData(typeof(int), typeof(Avg<int>))]
    [InlineData(typeof(long), typeof(Avg<long>))]
    [InlineData(typeof(decimal), typeof(AvgDecimal))]
    public void Avg_binds_by_input_type(Type input, Type expected)
    {
        var function = Agg.Avg.Bind("Voltage", input);
        Assert.IsType(expected, function);
        Assert.Equal("Voltage.Avg", function.Name);
    }

    [Fact]
    public void Quantile_descriptors_bind_with_fixed_labels()
    {
        Assert.Equal("V.Median", Agg.Median.Bind("V", typeof(double)).Name);
        Assert.Equal("V.Q95", Agg.Q95.Bind("V", typeof(double)).Name);
        Assert.Equal("V.Q99", Agg.Q99.Bind("V", typeof(float)).Name);
        Assert.Equal("V.Q50", Agg.Quantile(0.5).Bind("V", typeof(float)).Name);
        Assert.Equal(0.95, Assert.IsType<Quantile<float>>(Agg.Q95.Bind("V", typeof(float))).Level);
        Assert.IsType<QuantileDecimal>(Agg.Median.Bind("V", typeof(decimal)));
    }

    [Fact]
    public void Min_max_and_last_return_the_input_type()
    {
        Assert.IsType<Min<DateTime>>(Agg.Min.Bind("T", typeof(DateTime)));
        Assert.IsType<Max<TimeSpan>>(Agg.Max.Bind("D", typeof(TimeSpan)));
        Assert.IsType<Last<string>>(Agg.Last.Bind("S", typeof(string)));
        Assert.IsType<Last<bool>>(Agg.Last.Bind("B", typeof(bool)));
        Assert.IsType<StdDev<decimal>>(Agg.StdDev.Bind("X", typeof(decimal)));
        Assert.IsType<TrueRatio>(Agg.TrueRatio.Bind("B", typeof(bool)));
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(DateTime))]
    public void Unsupported_input_types_fail_with_the_column_and_function(Type input)
    {
        var ex = Assert.Throws<AggregationException>(() => Agg.Avg.Bind("Status", input));
        Assert.Equal("Status", ex.Column);
        Assert.Equal("Status.Avg", ex.Function);
    }

    [Fact]
    public void As_and_with_output_return_copies()
    {
        var renamed = Agg.StdDev.As("Spread").WithOutput(OutputEncoding.ScaledNumber32(3));

        Assert.Null(Agg.StdDev.Label);
        Assert.Null(Agg.StdDev.Output);
        var function = renamed.Bind("Current", typeof(float));
        Assert.Equal("Spread", function.Name);
        Assert.Equal(OutputEncoding.ScaledNumber32(3), function.Output);
    }

    [Theory]
    [InlineData(typeof(double), "X.Avg")]
    [InlineData(typeof(decimal), "X.Avg")]
    [InlineData(typeof(bool), "X.TrueRatio")]
    [InlineData(typeof(string), "X.Last")]
    public void Default_depends_on_the_column_type(Type input, string label)
    {
        Assert.Equal(label, Agg.Default.Bind("X", input).Name);
        Assert.Null(Agg.Default.LabelFor("X"));
        Assert.Equal("Mine", Agg.Default.As("Mine").Bind("X", input).Name);
    }

    [Fact]
    public void Default_rejects_time_columns()
    {
        Assert.Throws<AggregationException>(() => Agg.Default.Bind("T", typeof(DateTime)));
    }
}
