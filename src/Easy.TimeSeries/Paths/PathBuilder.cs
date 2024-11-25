namespace Easy.TimeSeries.Paths;

using System.Globalization;
using System.Text.RegularExpressions;

public sealed class PathBuilder
{
    private readonly string root;
    private readonly TimeGranularity granularity;
    private readonly string suffix;
    private readonly string dateFmt;
    private readonly Regex rgx;

    public PathBuilder(string root, TimeGranularity granularity, string suffix = "dat")
    {
        this.root = root;
        this.granularity = granularity;
        this.suffix = suffix;
        dateFmt = GetDateTimeFormatString(granularity);
        var pattern = GetPattern(root, granularity, suffix);
        rgx = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    private static string GetDateTimeFormatString(TimeGranularity granularity)
    {
        return granularity switch
        {
            TimeGranularity.Hour => "yyyy/MM/dd/HH",
            TimeGranularity.Day => "yyyy/MM/dd",
            TimeGranularity.Month => "yyyy/MM",
            TimeGranularity.Year => "yyyy",
            _ => throw new NotImplementedException(),
        };
    }

    private static string GetPattern(string root, TimeGranularity granularity, string suffix)
    {
        var datePattern = granularity switch
        {
            TimeGranularity.Hour => "20[0-9]{2}/(0[1-9]|1[0-2])/(0[1-9]|[1-2][0-9]|3[0-1])/(0[1-9]|2[0-3])",
            TimeGranularity.Day => "20[0-9]{2}/(0[1-9]|1[0-2])/(0[1-9]|[1-2][0-9]|3[0-1])",
            TimeGranularity.Month => "20[0-9]{2}/(0[1-9]|1[0-2])",
            TimeGranularity.Year => "20[0-9]{2}",
            _ => throw new NotImplementedException(),
        };

        var escapedRoot = Regex.Escape(root);
        var escapedSuffix = Regex.Escape(suffix);
        return $"^{escapedRoot}/(?<Date>{datePattern})/(?<SubjectId>[a-z0-9_-]+)\\.{escapedSuffix}$";
    }

    public bool TryParse(string path, out DateTime dateTime, out string subjectId)
    {
        var match = rgx.Match(path);
        if (!match.Success)
        {
            dateTime = DateTime.MinValue;
            subjectId = string.Empty;
            return false;
        }

        var dateStr = match.Groups["Date"].Value;
        var dateTimeUnspecified = DateTime.ParseExact(dateStr, dateFmt, CultureInfo.InvariantCulture);
        dateTime = DateTime.SpecifyKind(dateTimeUnspecified, DateTimeKind.Utc);
        subjectId = match.Groups["SubjectId"].Value;
        return true;
    }

    public (ICollection<string> paths, string prefix) GetExpectedPaths(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive)
    {
        Expect.Utc(fromUtcInclusive);
        Expect.Utc(toUtcExclusive);
        if (!IsDateDifferenceEnough(fromUtcInclusive, toUtcExclusive))
        {
            throw new ArgumentException("Date difference is not big enough.", nameof(toUtcExclusive));
        }

        var sequence = GetDateTimeSequence(fromUtcInclusive);

        var paths = new List<string>();
        var first = DateTime.MinValue;
        var firstSet = false;
        var last = DateTime.MinValue;
        foreach (var dt in sequence)
        {
            if (dt >= toUtcExclusive)
            {
                break;
            }

            if (!firstSet)
            {
                first = dt;
                firstSet = true;
            }

            var path = Path.Combine(root, dt.ToString(dateFmt));
            paths.Add(path);
            last = dt;
        }

        var prefix = GetPrefix(first, last);

        return (paths, prefix);
    }

    private IEnumerable<DateTime> GetDateTimeSequence(DateTime fromUtcInclusive)
    {
        var start = Start(fromUtcInclusive, granularity);
        var sequence = granularity switch
        {
            TimeGranularity.Hour => HourSequence(start),
            TimeGranularity.Day => DaySequence(start),
            TimeGranularity.Month => MonthSequence(start.Year, start.Month),
            TimeGranularity.Year => YearSequence(start.Year),
            _ => throw new NotImplementedException(),
        };
        return sequence;
    }

    private bool IsDateDifferenceEnough(DateTime from, DateTime to)
    {
        var diff = to - from;
        if (diff <= TimeSpan.Zero)
        {
            return false;
        }

        return granularity switch
        {
            TimeGranularity.Hour => diff >= TimeSpan.FromHours(1),
            TimeGranularity.Day => diff >= TimeSpan.FromDays(1),
            TimeGranularity.Month => (from.Year * 12) + from.Month < (to.Year * 12) + to.Month,
            TimeGranularity.Year => from.Year < to.Year,
            _ => false,
        };
    }

    private static DateTime Start(DateTime from, TimeGranularity granularity)
    {
        return granularity switch
        {
            TimeGranularity.Hour => from.BeginingOfHourUtc(),
            TimeGranularity.Day => from.MidnightUtc(),
            TimeGranularity.Month => from.BeginingOfMonthUtc(),
            TimeGranularity.Year => from.BeginingOfYearUtc(),
            _ => throw new NotImplementedException(),
        };
    }

    private static IEnumerable<DateTime> YearSequence(int year)
    {
        while (year > 0)
        {
            yield return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            ++year;
        }
    }

    private static IEnumerable<DateTime> MonthSequence(int year, int month)
    {
        while (year > 0)
        {
            yield return new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            if (month == 12)
            {
                ++year;
                month = 1;
                continue;
            }

            ++month;
        }
    }

    private static IEnumerable<DateTime> DaySequence(DateTime dt)
    {
        while (dt < DateTime.MaxValue)
        {
            yield return dt;
            dt = dt.AddDays(1);
        }
    }

    private static IEnumerable<DateTime> HourSequence(DateTime dt)
    {
        while (dt < DateTime.MaxValue)
        {
            yield return dt;
            dt = dt.AddHours(1);
        }
    }

    private string GetPrefix(DateTime first, DateTime last)
    {
        var firstStr = first.ToString(dateFmt);
        var lastStr = last.ToString(dateFmt);
        for (var i = 0; i < firstStr.Length; i++)
        {
            if (firstStr[i] == lastStr[i])
            {
                continue;
            }

            return Path.Combine(root, firstStr[..i]);
        }

        return string.Empty;
    }
}
