namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Paths;

public class ContractTests
{
    [Theory]
    [InlineData(AggregationTimeSpan.Second, "1s", 1)]
    [InlineData(AggregationTimeSpan.TenSeconds, "10s", 10)]
    [InlineData(AggregationTimeSpan.Minute, "1m", 60)]
    [InlineData(AggregationTimeSpan.FiveMinutes, "5m", 300)]
    [InlineData(AggregationTimeSpan.FifteenMinutes, "15m", 900)]
    [InlineData(AggregationTimeSpan.Hour, "1h", 3600)]
    [InlineData(AggregationTimeSpan.Day, "1d", 86400)]
    public void Time_span_slug_and_length(AggregationTimeSpan span, string slug, int seconds)
    {
        Assert.Equal(slug, span.ToSlug());
        Assert.Equal(TimeSpan.FromSeconds(seconds), span.ToTimeSpan());
        Assert.Equal(0, TimeSpan.FromDays(1).Ticks % span.ToTimeSpan().Ticks);
    }

    [Fact]
    public void Slice_start_is_aligned_to_midnight()
    {
        var t = new DateTime(2026, 10, 25, 14, 7, 31, 250, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 10, 25, 14, 7, 30, DateTimeKind.Utc), AggregationTimeSpan.TenSeconds.GetSliceStart(t));
        Assert.Equal(new DateTime(2026, 10, 25, 14, 0, 0, DateTimeKind.Utc), AggregationTimeSpan.FifteenMinutes.GetSliceStart(t));
        Assert.Equal(new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc), AggregationTimeSpan.Day.GetSliceStart(t));
        Assert.Throws<ArgumentException>(() => AggregationTimeSpan.Minute.GetSliceStart(DateTime.SpecifyKind(t, DateTimeKind.Local)));
    }

    [Fact]
    public void Storage_words_are_spelled_out()
    {
        Assert.Equal("hourly", TimeGranularity.Hour.ToWord());
        Assert.Equal("monthly", TimeGranularity.Month.ToWord());
    }

    [Fact]
    public void Output_encodings_validate_their_meta()
    {
        Assert.Equal(3, OutputEncoding.ScaledNumber32(3).Meta);
        Assert.Throws<ArgumentOutOfRangeException>(() => OutputEncoding.ScaledNumber64(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => OutputEncoding.ScaledNumber64(9));
        Assert.Equal((int)TimePrecision.Seconds, OutputEncoding.DateTimeUnordered(TimePrecision.Seconds).Meta);
        Assert.True(OutputEncoding.SameAsInput.IsInherited);
        Assert.True(OutputEncoding.FromInputPrecision.IsInherited);
        Assert.False(OutputEncoding.Double.IsInherited);
        Assert.NotEqual(OutputEncoding.SameAsInput, OutputEncoding.FromInputPrecision);
    }

    [Fact]
    public void Inputs_accept_only_decoded_types()
    {
        Assert.Equal(typeof(float), AggregationInput.Of<float>("Voltage").ValueType);
        Assert.Equal(2, AggregationInput.Of<string>(2).ColumnIndex);
        Assert.Throws<ArgumentException>(() => AggregationInput.Of<short>("Voltage"));
        Assert.Throws<ArgumentException>(() => AggregationInput.Of<DateTimeOffset>("Voltage"));
        Assert.Throws<ArgumentException>(() => AggregationInput.Of<int>(string.Empty));
    }

    [Fact]
    public void Slice_exposes_aligned_values_and_times()
    {
        var (count, values, times) = Slices.Over(
            s => (s.Count, s.Values<double>(1).ToArray(), s.Times.ToArray()),
            new float[] { 1f, 2f },
            new double[] { 10d, 20d });

        Assert.Equal(2, count);
        Assert.Equal([10d, 20d], values);
        Assert.Equal([Slices.Start, Slices.Start.AddSeconds(1)], times);
    }

    [Fact]
    public void Values_with_a_wrong_type_throw()
    {
        Assert.Throws<InvalidOperationException>(
            () => Slices.Over(s => s.Values<double>(0).Length, new float[] { 1f }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Slices.Over(s => s.Values<float>(1).Length, new float[] { 1f }));
    }

    [Fact]
    public void Sorted_excludes_nan_and_is_shared_within_a_slice()
    {
        var (first, second, same) = Slices.Over(
            s =>
            {
                var a = s.Sorted<double>(0);
                var b = s.Sorted<double>(0);
                return (a.ToArray(), b.ToArray(), a == b);
            },
            new[] { 3d, double.NaN, 1d, double.PositiveInfinity, 2d });

        Assert.Equal([1d, 2d, 3d, double.PositiveInfinity], first);
        Assert.Equal(first, second);
        Assert.True(same, "the second call should return the cached copy");
    }

    [Fact]
    public void Sorted_of_all_nan_is_empty()
    {
        Assert.Equal(0, Slices.Over(s => s.Sorted<float>(0).Length, new[] { float.NaN, float.NaN }));
    }

    [Fact]
    public void Project_remaps_one_input_to_index_zero()
    {
        var values = Slices.Over(
            s => s.Project(1).Values<double>(0).ToArray(),
            new float[] { 1f },
            new double[] { 42d });

        Assert.Equal([42d], values);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Slices.Over(s => s.Project(1).Values<double>(1).Length, new float[] { 1f }, new double[] { 1d }));
    }
}
