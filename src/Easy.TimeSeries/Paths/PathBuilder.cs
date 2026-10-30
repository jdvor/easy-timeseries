namespace Easy.TimeSeries.Paths;

using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Maps between UTC timestamps and the date-partitioned file paths a time series is stored under
/// (e.g. <c>root/2026/07/06/subject.ets</c> at day granularity). Builds the paths that cover a time range for
/// reading and parses an existing path back into its timestamp and subject id.
/// </summary>
/// <remarks>
/// Paths are logical storage paths: always joined with <c>/</c>, whatever the OS, and relative to a container or
/// directory. Subject ids are lowercase (<c>[a-z0-9_-]+</c>) and matched case-sensitively, so two blobs differing only
/// in case never parse to the same subject.
/// </remarks>
public sealed partial class PathBuilder
{
    private const char Separator = '/';
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private readonly string root;
    private readonly TimeGranularity granularity;
    private readonly string suffix;
    private readonly string dateFmt;
    private readonly string nonSegmentedDateFmt;
    private readonly Regex rgx;

    /// <summary>Creates a builder rooted at <paramref name="root"/> that partitions by <paramref name="granularity"/>.</summary>
    /// <param name="root">Logical path prefix, segments separated by <c>/</c>; may be empty.</param>
    /// <param name="suffix">File extension without the dot; defaults to <c>ets</c>.</param>
    public PathBuilder(string root, TimeGranularity granularity, string suffix = "ets")
    {
        Expect.NotNull(root);
        this.root = root.TrimEnd(Separator);
        this.granularity = granularity;
        this.suffix = suffix;
        dateFmt = GetDateTimeFormatString(granularity);
        nonSegmentedDateFmt = GetNonSegmentedDateTimeFormatString(granularity);
        var pattern = GetPattern(this.root, granularity, suffix);
        rgx = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    public PathBuilder(TimeGranularity granularity)
        : this(string.Empty, granularity)
    {
    }

    /// <summary>Logical path prefix without a trailing <c>/</c>; empty when the files live at the container root.</summary>
    public string Root => root;

    /// <summary>Size of the time bucket one file covers.</summary>
    public TimeGranularity Granularity => granularity;

    /// <summary>Whether <paramref name="subjectId"/> can be used in a path: lowercase letters, digits, <c>_</c> and <c>-</c>.</summary>
    public static bool IsValidSubjectId(string subjectId)
        => !string.IsNullOrEmpty(subjectId) && SubjectIdRegex().IsMatch(subjectId);

    /// <summary>Start of the bucket containing <paramref name="utc"/>.</summary>
    public DateTime GetBucketStart(DateTime utc)
    {
        Expect.Utc(utc);
        return Start(utc, granularity);
    }

    /// <summary>Exclusive end of the bucket containing <paramref name="bucketStart"/>, i.e. the start of the next one.</summary>
    public DateTime GetBucketEnd(DateTime bucketStart)
    {
        Expect.Utc(bucketStart);
        return granularity switch
        {
            TimeGranularity.Hour => bucketStart.BeginningOfNextHourUtc(),
            TimeGranularity.Day => bucketStart.NextMidnightUtc(),
            TimeGranularity.Month => bucketStart.BeginningOfNextMonthUtc(),
            TimeGranularity.Year => bucketStart.BeginningOfNextYearUtc(),
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Starts of the buckets overlapping <paramref name="fromUtcInclusive"/> to <paramref name="toUtcExclusive"/>, in
    /// ascending order. Empty when the range is empty.
    /// </summary>
    public IEnumerable<DateTime> GetBucketStarts(DateTime fromUtcInclusive, DateTime toUtcExclusive)
    {
        Expect.Utc(fromUtcInclusive);
        Expect.Utc(toUtcExclusive);
        return fromUtcInclusive < toUtcExclusive ? Iterate(Start(fromUtcInclusive, granularity), toUtcExclusive) : [];

        IEnumerable<DateTime> Iterate(DateTime start, DateTime end)
        {
            for (var dt = start; dt < end; dt = GetBucketEnd(dt))
            {
                yield return dt;
            }
        }
    }

    /// <summary>
    /// Path of the file holding <paramref name="subjectId"/> in the bucket containing <paramref name="bucketStart"/>:
    /// <c>{root}/{date folders}/{subjectId}.{suffix}</c>. The inverse of <see cref="TryParse"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The subject id is not valid, see <see cref="IsValidSubjectId"/>.</exception>
    public string GetFilePath(DateTime bucketStart, string subjectId)
    {
        Expect.Utc(bucketStart);
        if (!IsValidSubjectId(subjectId))
        {
            throw new ArgumentException(
                $"Subject id '{subjectId}' must be non-empty and contain only lowercase letters, digits, '_' and '-'.",
                nameof(subjectId));
        }

        return Join(root, $"{bucketStart.ToString(dateFmt, Culture)}/{subjectId}.{suffix}");
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

    private static string GetNonSegmentedDateTimeFormatString(TimeGranularity granularity)
    {
        return granularity switch
        {
            TimeGranularity.Hour => "yyyyMMddHH",
            TimeGranularity.Day => "yyyyMMdd",
            TimeGranularity.Month => "yyyyMM",
            TimeGranularity.Year => "yyyy",
            _ => throw new NotImplementedException(),
        };
    }

    private static string GetPattern(string root, TimeGranularity granularity, string suffix)
    {
        var datePattern = granularity switch
        {
            TimeGranularity.Hour => "20[0-9]{2}/(0[1-9]|1[0-2])/(0[1-9]|[1-2][0-9]|3[0-1])/([01][0-9]|2[0-3])",
            TimeGranularity.Day => "20[0-9]{2}/(0[1-9]|1[0-2])/(0[1-9]|[1-2][0-9]|3[0-1])",
            TimeGranularity.Month => "20[0-9]{2}/(0[1-9]|1[0-2])",
            TimeGranularity.Year => "20[0-9]{2}",
            _ => throw new NotImplementedException(),
        };

        var escapedRoot = root.Length == 0 ? string.Empty : $"{Regex.Escape(root)}/";
        var escapedSuffix = Regex.Escape(suffix);
        return $"^{escapedRoot}(?<Date>{datePattern})/(?<SubjectId>[a-z0-9_-]+)\\.{escapedSuffix}\\z";
    }

    /// <summary>Parses a storage path back into its UTC <paramref name="dateTime"/> bucket and <paramref name="subjectId"/>. Returns <c>false</c> if the path does not match this builder's layout.</summary>
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
        var dateTimeUnspecified = DateTime.ParseExact(dateStr, dateFmt, Culture);
        dateTime = DateTime.SpecifyKind(dateTimeUnspecified, DateTimeKind.Utc);
        subjectId = match.Groups["SubjectId"].Value;
        return true;
    }

    /// <summary>
    /// Enumerate the partition paths covering <paramref name="fromUtcInclusive"/> to <paramref name="toUtcExclusive"/>,
    /// one per granularity bucket, plus their longest common path prefix (useful as a listing filter). Both bounds must
    /// be UTC and span at least one bucket.
    /// </summary>
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

            var path = Join(root, dt.ToString(dateFmt, Culture));
            paths.Add(path);
            last = dt;
        }

        var prefix = GetPrefix(first, last);

        return (paths, prefix);
    }

    public string GetExpectedFilePath(DateTime fromUtcInclusive, string? fileName = null)
    {
        var path = Join(root, fromUtcInclusive.ToString(dateFmt, Culture));
        fileName ??= $"{fromUtcInclusive.ToString(nonSegmentedDateFmt, Culture)}.{suffix}";
        return Join(path, fileName);
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
            TimeGranularity.Hour => from.BeginningOfHourUtc(),
            TimeGranularity.Day => from.MidnightUtc(),
            TimeGranularity.Month => from.BeginningOfMonthUtc(),
            TimeGranularity.Year => from.BeginningOfYearUtc(),
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
        var firstStr = first.ToString(dateFmt, Culture);
        var lastStr = last.ToString(dateFmt, Culture);
        for (var i = 0; i < firstStr.Length; i++)
        {
            if (firstStr[i] == lastStr[i])
            {
                continue;
            }

            return Join(root, firstStr[..i]);
        }

        // A single bucket: the full path of its folder, so a listing never scans more than that bucket.
        return Join(root, $"{firstStr}/");
    }

    private static string Join(string left, string right)
        => left.Length == 0 ? right : $"{left}{Separator}{right}";

    // \z, not $: $ also matches before a final newline.
    [GeneratedRegex(@"^[a-z0-9_-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex SubjectIdRegex();
}
