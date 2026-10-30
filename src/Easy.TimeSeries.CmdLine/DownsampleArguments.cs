namespace Easy.TimeSeries.CmdLine;

using Easy.TimeSeries.Aggregation;
using Easy.TimeSeries.Paths;
using System.Globalization;

/// <summary>Parsed arguments of <c>ets downsample</c>.</summary>
internal sealed record DownsampleOptions(
    DirectoryInfo Input,
    DirectoryInfo Output,
    TimeGranularity RawGranularity,
    AggregationSpec Spec,
    DateTime From,
    DateTime To,
    string? Subject,
    bool Force);

/// <summary>Parsing of the <c>ets downsample</c> arguments; every failure is an <see cref="ArgumentException"/>.</summary>
internal static class DownsampleArguments
{
    public static DownsampleOptions Parse(
        string input,
        string output,
        string rawGranularity,
        string aggregation,
        string storage,
        string from,
        string to,
        string columns,
        string? group = null,
        string? subject = null,
        string? time = null,
        bool force = false)
    {
        var fromUtc = ParseUtc(from, "--from");
        var toUtc = ParseUtc(to, "--to");
        if (fromUtc >= toUtc)
        {
            throw new ArgumentException($"--from ({from}) must be earlier than --to ({to}).");
        }

        if (subject is not null && !PathBuilder.IsValidSubjectId(subject))
        {
            throw new ArgumentException($"--subject '{subject}' must contain only lowercase letters, digits, '_' and '-'.");
        }

        var spec = new AggregationSpec(ParseAggregation(aggregation), ParseGranularity(storage, "--storage"));
        if (group is not null)
        {
            spec = spec.GroupBy(group);
        }

        if (time is not null)
        {
            spec = spec.Time(time);
        }

        return new DownsampleOptions(
            new DirectoryInfo(input),
            new DirectoryInfo(output),
            ParseGranularity(rawGranularity, "--raw-granularity"),
            ApplyColumns(spec, columns),
            fromUtc,
            toUtc,
            subject,
            force);
    }

    /// <summary>One of <c>1s</c>, <c>10s</c>, <c>1m</c>, <c>5m</c>, <c>15m</c>, <c>1h</c>, <c>1d</c>.</summary>
    public static AggregationTimeSpan ParseAggregation(string slug)
    {
        foreach (var value in Enum.GetValues<AggregationTimeSpan>())
        {
            if (string.Equals(value.ToSlug(), slug, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        throw new ArgumentException(
            $"--aggregation '{slug}' is not one of: {string.Join(", ", Enum.GetValues<AggregationTimeSpan>().Select(x => x.ToSlug()))}.");
    }

    /// <summary><c>hour</c>, <c>day</c>, <c>month</c> or <c>year</c>.</summary>
    public static TimeGranularity ParseGranularity(string value, string option)
        => value.ToLowerInvariant() switch
        {
            "hour" => TimeGranularity.Hour,
            "day" => TimeGranularity.Day,
            "month" => TimeGranularity.Month,
            "year" => TimeGranularity.Year,
            _ => throw new ArgumentException($"{option} '{value}' is not one of: hour, day, month, year."),
        };

    /// <summary>A date or date and time, read as UTC: <c>2026-10-01</c>, <c>2026-10-01T06:00:00Z</c>.</summary>
    public static DateTime ParseUtc(string value, string option)
        => DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var result)
            ? result
            : throw new ArgumentException($"{option} '{value}' is not a date, e.g. 2026-10-01 or 2026-10-01T06:00:00Z.");

    /// <summary>
    /// <c>Voltage=avg+min+q99.9,Current,Status=last</c>: columns separated by commas, functions by <c>+</c>. A column
    /// without functions gets the default of its type.
    /// </summary>
    public static AggregationSpec ApplyColumns(AggregationSpec spec, string columns)
    {
        var any = false;
        foreach (var item in columns.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = item.IndexOf('=', StringComparison.Ordinal);
            var label = separator < 0 ? item : item[..separator].Trim();
            if (label.Length == 0)
            {
                throw new ArgumentException($"--columns item '{item}' has no column name.");
            }

            var functions = separator < 0
                ? []
                : item[(separator + 1)..]
                    .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(ParseFunction)
                    .ToArray();
            if (separator >= 0 && functions.Length == 0)
            {
                throw new ArgumentException($"--columns item '{item}' names no functions after '='.");
            }

            spec = spec.Column(label, functions);
            any = true;
        }

        return any ? spec : throw new ArgumentException("--columns names no columns.");
    }

    /// <summary><c>avg</c>, <c>median</c>, <c>min</c>, <c>max</c>, <c>stddev</c>, <c>trueratio</c>, <c>last</c>, <c>q95</c>, <c>q99</c>, <c>q{percent}</c>.</summary>
    public static AggregationDescriptor ParseFunction(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "avg": return Agg.Avg;
            case "median": return Agg.Median;
            case "min": return Agg.Min;
            case "max": return Agg.Max;
            case "stddev": return Agg.StdDev;
            case "trueratio": return Agg.TrueRatio;
            case "last": return Agg.Last;
            case "q95": return Agg.Q95;
            case "q99": return Agg.Q99;
        }

        if (name.Length > 1
            && (name[0] is 'q' or 'Q')
            && decimal.TryParse(name.AsSpan(1), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var percent)
            && percent <= 100)
        {
            return Agg.Quantile((double)(percent / 100m));
        }

        throw new ArgumentException(
            $"Function '{name}' is not one of: avg, median, min, max, stddev, trueratio, last, q95, q99, q<percent> (e.g. q99.9).");
    }
}
