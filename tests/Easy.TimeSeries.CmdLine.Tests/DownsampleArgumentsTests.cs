namespace Easy.TimeSeries.CmdLine.Tests;

using Easy.TimeSeries.Aggregation;
using Easy.TimeSeries.Paths;

public class DownsampleArgumentsTests
{
    [Theory]
    [InlineData("1s", AggregationTimeSpan.Second)]
    [InlineData("10s", AggregationTimeSpan.TenSeconds)]
    [InlineData("1M", AggregationTimeSpan.Minute)]
    [InlineData("15m", AggregationTimeSpan.FifteenMinutes)]
    [InlineData("1d", AggregationTimeSpan.Day)]
    public void Aggregation_slugs_parse(string slug, AggregationTimeSpan expected)
    {
        Assert.Equal(expected, DownsampleArguments.ParseAggregation(slug));
    }

    [Theory]
    [InlineData("2m")]
    [InlineData("minute")]
    public void Unknown_slug_lists_the_valid_ones(string slug)
    {
        var ex = Assert.Throws<ArgumentException>(() => DownsampleArguments.ParseAggregation(slug));
        Assert.Contains("15m", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Granularity_parses_case_insensitively()
    {
        Assert.Equal(TimeGranularity.Month, DownsampleArguments.ParseGranularity("Month", "--storage"));
        Assert.Throws<ArgumentException>(() => DownsampleArguments.ParseGranularity("daily", "--storage"));
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-01T00:00:00Z")]
    [InlineData("2026-10-01T06:30:00Z", "2026-10-01T06:30:00Z")]
    [InlineData("2026-10-01T08:30:00+02:00", "2026-10-01T06:30:00Z")]
    public void Dates_are_utc(string value, string expected)
    {
        var parsed = DownsampleArguments.ParseUtc(value, "--from");
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime(), parsed);
    }

    [Fact]
    public void Columns_parse_into_the_spec()
    {
        var spec = DownsampleArguments.ApplyColumns(
            new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Day),
            "Voltage=avg+min+max+q95+q99.9, Current ,Status=last,Ok=trueratio");

        var header = new Header(
            Version.V1,
            [
                new ColumnInfo(0, ColumnValueType.DateTimeOrdered, 0, "t"),
                new ColumnInfo(1, ColumnValueType.Double, 0, "Voltage"),
                new ColumnInfo(2, ColumnValueType.Double, 0, "Current"),
                new ColumnInfo(3, ColumnValueType.Category, 0, "Status"),
                new ColumnInfo(4, ColumnValueType.Bool, 0, "Ok"),
            ]);
        Assert.Equal(
            ["t", "Count", "Voltage.Avg", "Voltage.Min", "Voltage.Max", "Voltage.Q95", "Voltage.Q99.9", "Current.Avg", "Status.Last", "Ok.TrueRatio"],
            spec.Describe([header]).Select(x => x.Label));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Voltage=")]
    [InlineData("=avg")]
    [InlineData("Voltage=mean")]
    [InlineData("Voltage=q101")]
    [InlineData("Voltage=avg,Voltage=max")]
    public void Bad_columns_fail(string columns)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => DownsampleArguments.ApplyColumns(new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Day), columns));
    }

    [Fact]
    public void Parse_checks_range_and_subject()
    {
        Assert.Throws<ArgumentException>(() => Parse(from: "2026-10-02", to: "2026-10-01"));
        Assert.Throws<ArgumentException>(() => Parse(subject: "Sensor-A"));

        var options = Parse(subject: "sensor-a");
        Assert.Equal(("sensor-a", TimeGranularity.Hour, "Sensor"), (options.Subject, options.RawGranularity, options.Spec.GroupColumn));
    }

    private static DownsampleOptions Parse(string from = "2026-10-01", string to = "2026-10-02", string? subject = null)
        => DownsampleArguments.Parse("in", "out", "hour", "1m", "day", from, to, "V", group: "Sensor", subject: subject);
}
