namespace Easy.TimeSeries.Aggregation;

using Easy.TimeSeries.Paths;

/// <summary>
/// How much raw time is collapsed into one aggregated row. Every value divides a day evenly; slices are aligned to
/// midnight UTC and half-open (start inclusive, end exclusive).
/// </summary>
public enum AggregationTimeSpan
{
    /// <summary>One second.</summary>
    Second = 1,

    /// <summary>Ten seconds.</summary>
    TenSeconds,

    /// <summary>One minute.</summary>
    Minute,

    /// <summary>Five minutes.</summary>
    FiveMinutes,

    /// <summary>Fifteen minutes.</summary>
    FifteenMinutes,

    /// <summary>One hour.</summary>
    Hour,

    /// <summary>One day.</summary>
    Day,
}

/// <summary>Conversions and slice alignment for <see cref="AggregationTimeSpan"/>.</summary>
public static class AggregationTimeSpanExtensions
{
    /// <summary>Length of the aggregation time span.</summary>
    public static TimeSpan ToTimeSpan(this AggregationTimeSpan value)
        => value switch
        {
            AggregationTimeSpan.Second => TimeSpan.FromSeconds(1),
            AggregationTimeSpan.TenSeconds => TimeSpan.FromSeconds(10),
            AggregationTimeSpan.Minute => TimeSpan.FromMinutes(1),
            AggregationTimeSpan.FiveMinutes => TimeSpan.FromMinutes(5),
            AggregationTimeSpan.FifteenMinutes => TimeSpan.FromMinutes(15),
            AggregationTimeSpan.Hour => TimeSpan.FromHours(1),
            AggregationTimeSpan.Day => TimeSpan.FromDays(1),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown aggregation time span."),
        };

    /// <summary>Short name used in aggregated roots: <c>1s</c>, <c>10s</c>, <c>1m</c>, <c>5m</c>, <c>15m</c>, <c>1h</c>, <c>1d</c>.</summary>
    public static string ToSlug(this AggregationTimeSpan value)
        => value switch
        {
            AggregationTimeSpan.Second => "1s",
            AggregationTimeSpan.TenSeconds => "10s",
            AggregationTimeSpan.Minute => "1m",
            AggregationTimeSpan.FiveMinutes => "5m",
            AggregationTimeSpan.FifteenMinutes => "15m",
            AggregationTimeSpan.Hour => "1h",
            AggregationTimeSpan.Day => "1d",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown aggregation time span."),
        };

    /// <summary>Start of the aggregation slice containing <paramref name="utc"/>.</summary>
    public static DateTime GetSliceStart(this AggregationTimeSpan value, DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Date & time must be in UTC.", nameof(utc));
        }

        // DateTime ticks start at midnight, and every aggregation time span divides a day.
        var ticks = value.ToTimeSpan().Ticks;
        return new DateTime(utc.Ticks - (utc.Ticks % ticks), DateTimeKind.Utc);
    }

    internal static bool IsDefined(this AggregationTimeSpan value)
        => value is >= AggregationTimeSpan.Second and <= AggregationTimeSpan.Day;

    /// <summary>Word used in aggregated roots, spelled out so that "month" never collides with the "m" of minutes.</summary>
    internal static string ToWord(this TimeGranularity granularity)
        => granularity switch
        {
            TimeGranularity.Hour => "hourly",
            TimeGranularity.Day => "daily",
            TimeGranularity.Month => "monthly",
            TimeGranularity.Year => "yearly",
            _ => throw new ArgumentOutOfRangeException(nameof(granularity), granularity, "Unknown storage time span."),
        };

    /// <summary>Shortest length of one bucket; months and years are never shorter than a day.</summary>
    internal static TimeSpan MinLength(this TimeGranularity granularity)
        => granularity == TimeGranularity.Hour ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
}
