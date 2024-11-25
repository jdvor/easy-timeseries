namespace Easy.TimeSeries.Tests;

using System.Collections.Immutable;
using System.Text.RegularExpressions;

/// <summary>
/// Easier building of series consisting of (usually incremental) DateTime instances.
/// </summary>
internal sealed class DateTimeSeriesBuilder
{
    private static readonly Regex IntervalRgx = new(@"^(?<val>\d+)(?<unit>(ms|s|m|h|d))$", RegexOptions.Compiled);
    private readonly List<DateTime> series = new(50);

    public DateTimeSeriesBuilder Append(DateTime dt)
    {
        series.Add(dt.ToUniversalTime());
        return this;
    }

    public DateTimeSeriesBuilder Append(string dateStr)
        => Append(DateTime.Parse(dateStr));

    public DateTimeSeriesBuilder Append(DateTime dt, int count)
    {
        for (var i = 0; i < count; i++)
        {
            series.Add(dt);
        }

        return this;
    }

    public DateTimeSeriesBuilder AddNow()
    {
        series.Add(DateTime.UtcNow);
        return this;
    }

    public DateTimeSeriesBuilder Append(TimeSpan interval)
    {
        return series.Count > 0
            ? Append(series.Last().Add(interval))
            : AddNow();
    }

    public DateTimeSeriesBuilder Append(TimeSpan interval, int count)
    {
        if (series.Count == 0)
        {
            series.Add(DateTime.UtcNow);
        }

        var curr = series.Last();
        for (var i = 0; i < count; i++)
        {
            curr = curr.Add(interval);
            series.Add(curr);
        }

        return this;
    }

    public DateTimeSeriesBuilder Append(params TimeSpan[] intervals)
    {
        if (intervals.Length == 0)
        {
            return this;
        }

        if (series.Count == 0)
        {
            series.Add(DateTime.UtcNow);
        }

        var curr = series.Last();
        foreach (var interval in intervals)
        {
            curr = curr.Add(interval);
            series.Add(curr);
        }

        return this;
    }

    public DateTimeSeriesBuilder AppendIntervals(params string[] intervals)
    {
        if (intervals.Length == 0)
        {
            return this;
        }

        if (series.Count == 0)
        {
            series.Add(DateTime.UtcNow);
        }

        var curr = series.Last();
        foreach (var interval in intervals)
        {
            curr = curr.Add(ParseInterval(interval));
            series.Add(curr);
        }

        return this;
    }

    private static TimeSpan ParseInterval(string s)
    {
        if (s.Contains(':'))
        {
            return TimeSpan.Parse(s);
        }

        var match = IntervalRgx.Match(s);
        if (!match.Success)
        {
            throw new Exception();
        }

        var value = int.Parse(match.Groups["val"].Value);
        var unit = match.Groups["unit"].Value;
        return unit switch
        {
            "d" => new TimeSpan(value, 0, 0, 0),
            "h" => new TimeSpan(0, value, 0, 0),
            "m" => new TimeSpan(0, 0, value, 0),
            "s" => new TimeSpan(0, 0, 0, value),
            "ms" => new TimeSpan(0, 0, 0, 0, value),
            _ => throw new Exception(),
        };
    }

    public DateTimeSeriesBuilder AppendDates(params string[] dates)
    {
        if (dates.Length == 0)
        {
            return this;
        }

        foreach (var dateStr in dates)
        {
            var dt = DateTime.Parse(dateStr).ToUniversalTime();
            series.Add(dt);
        }

        return this;
    }

    public ImmutableArray<DateTime> Build()
        => ImmutableArray.Create(series.ToArray());
}
