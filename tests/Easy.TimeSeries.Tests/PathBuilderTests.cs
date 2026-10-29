namespace Easy.TimeSeries.Tests;

using Paths;

public class PathBuilderTests : IClassFixture<PathBuilderTests.Fixture>
{
    private const string Root = ".cache/timeseries/raw";

    private readonly Fixture fixture;

    public PathBuilderTests(PathBuilderTests.Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Day_builder_creates_correct_paths()
    {
        var pb = fixture.BuilderFor(TimeGranularity.Day);
        var from = new DateTime(2023, 12, 24, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var (paths, prefix) = pb.GetExpectedPaths(from, to);

        Assert.Equal(8, paths.Count);
        Assert.Equal($"{Root}/2023/12/", prefix);
    }

    [Fact]
    public void Hour_builder_creates_correct_paths()
    {
        var pb = fixture.BuilderFor(TimeGranularity.Hour);
        var from = new DateTime(2023, 12, 24, 10, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2023, 12, 24, 19, 0, 0, DateTimeKind.Utc);
        var (paths, prefix) = pb.GetExpectedPaths(from, to);

        Assert.Equal(9, paths.Count);
        Assert.Equal($"{Root}/2023/12/24/1", prefix);
    }

    [Theory]
    [MemberData(nameof(ParsePathData))]
    public void Parse_path(TestCase tc)
    {
        var pb = fixture.BuilderFor(tc.Granularity);
        var ok = pb.TryParse(tc.Path, out var time, out var subjectId);
        Assert.Equal(tc.ShouldSucceed, ok);
        if (ok)
        {
            Assert.True(tc.Time == time, "time differs");
            Assert.True(tc.SubjectId == subjectId, "subject IDs differ");
        }
    }

    [Theory]
    [MemberData(nameof(AllHoursData))]
    public void Parse_path_accepts_every_hour_of_the_day(int hour)
    {
        var pb = fixture.BuilderFor(TimeGranularity.Hour);
        var path = $"{Root}/2023/06/09/{hour:D2}/ab01.ets";

        var ok = pb.TryParse(path, out var time, out var subjectId);

        Assert.True(ok, $"hour {hour:D2} did not parse");
        Assert.Equal(new DateTime(2023, 6, 9, hour, 0, 0, DateTimeKind.Utc), time);
        Assert.Equal("ab01", subjectId);
    }

    [Fact]
    public void Builder_accepts_empty_root()
    {
        var pb = new PathBuilder(TimeGranularity.Day);
        var (paths, prefix) = pb.GetExpectedPaths(
            fromUtcInclusive: new DateTime(2026, 8, 2, 20, 00, 00, DateTimeKind.Utc),
            toUtcExclusive: new DateTime(2026, 8, 4, 03, 00, 00, DateTimeKind.Utc));
        Assert.Equal(3, paths.Count);
        Assert.Equal("2026/08/0", prefix);
    }

    [Fact]
    public void Builder_returns_correct_expected_path()
    {
        var pb = new PathBuilder(TimeGranularity.Hour);
        var path = pb.GetExpectedFilePath(
            fromUtcInclusive: new DateTime(2026, 8, 2, 20, 32, 47, 895, DateTimeKind.Utc));
        Assert.Equal("2026/08/02/20/2026080220.ets", path);
    }

    [Fact]
    public void Single_bucket_prefix_is_the_bucket_folder()
    {
        var pb = fixture.BuilderFor(TimeGranularity.Hour);
        var from = new DateTime(2023, 12, 24, 10, 0, 0, DateTimeKind.Utc);
        var (paths, prefix) = pb.GetExpectedPaths(from, from.AddHours(1));

        Assert.Equal([$"{Root}/2023/12/24/10"], paths);
        Assert.Equal($"{Root}/2023/12/24/10/", prefix);
    }

    [Theory]
    [InlineData(TimeGranularity.Hour, "2026/10/25/14/sensor-a.ets")]
    [InlineData(TimeGranularity.Day, "2026/10/25/sensor-a.ets")]
    [InlineData(TimeGranularity.Month, "2026/10/sensor-a.ets")]
    [InlineData(TimeGranularity.Year, "2026/sensor-a.ets")]
    public void File_path_round_trips_through_parse(TimeGranularity granularity, string datePart)
    {
        var at = new DateTime(2026, 10, 25, 14, 37, 12, DateTimeKind.Utc);
        foreach (var root in new[] { string.Empty, "raw", "agg-1m-daily", "a/b/c" })
        {
            var pb = new PathBuilder(root, granularity);
            var bucketStart = pb.GetBucketStart(at);
            var path = pb.GetFilePath(bucketStart, "sensor-a");

            Assert.Equal(root.Length == 0 ? datePart : $"{root}/{datePart}", path);
            Assert.True(pb.TryParse(path, out var parsedStart, out var subjectId), $"'{path}' did not parse");
            Assert.Equal(bucketStart, parsedStart);
            Assert.Equal(DateTimeKind.Utc, parsedStart.Kind);
            Assert.Equal("sensor-a", subjectId);
        }
    }

    [Fact]
    public void Root_with_trailing_separator_is_normalized()
    {
        var pb = new PathBuilder("raw/", TimeGranularity.Day);
        Assert.Equal("raw", pb.Root);
        Assert.Equal(
            "raw/2026/10/25/s1.ets",
            pb.GetFilePath(new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc), "s1"));
    }

    [Theory]
    [InlineData("sensor-a", true)]
    [InlineData("a_b-1", true)]
    [InlineData("", false)]
    [InlineData("Sensor-A", false)]
    [InlineData("a/b", false)]
    [InlineData("a.b", false)]
    [InlineData("a b", false)]
    [InlineData("sensor-a\n", false)]
    public void Subject_id_validation(string subjectId, bool expected)
    {
        Assert.Equal(expected, PathBuilder.IsValidSubjectId(subjectId));
    }

    [Fact]
    public void File_path_rejects_invalid_subject_id()
    {
        var pb = new PathBuilder("raw", TimeGranularity.Day);
        var at = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
        Assert.Throws<ArgumentException>(() => pb.GetFilePath(at, "Sensor-A"));
    }

    [Fact]
    public void Bucket_start_and_end_follow_the_granularity()
    {
        var at = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var next = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 12, 31, 23, 0, 0, DateTimeKind.Utc), new PathBuilder(TimeGranularity.Hour).GetBucketStart(at));
        Assert.Equal(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), new PathBuilder(TimeGranularity.Day).GetBucketStart(at));
        Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), new PathBuilder(TimeGranularity.Month).GetBucketStart(at));
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new PathBuilder(TimeGranularity.Year).GetBucketStart(at));

        foreach (var granularity in new[] { TimeGranularity.Hour, TimeGranularity.Day, TimeGranularity.Month, TimeGranularity.Year })
        {
            var pb = new PathBuilder(granularity);
            Assert.Equal(next, pb.GetBucketEnd(pb.GetBucketStart(at)));
        }
    }

    [Fact]
    public void Bucket_starts_cover_partially_overlapped_buckets()
    {
        var pb = new PathBuilder(TimeGranularity.Hour);
        var from = new DateTime(2026, 10, 25, 22, 30, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 26, 0, 0, 1, DateTimeKind.Utc);

        Assert.Equal(
            [
                new DateTime(2026, 10, 25, 22, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 25, 23, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
            ],
            pb.GetBucketStarts(from, to));
        Assert.Empty(pb.GetBucketStarts(to, from));
    }

    [Fact]
    public void Bucket_starts_of_an_empty_range_within_one_bucket_are_empty()
    {
        var pb = new PathBuilder(TimeGranularity.Hour);
        var at = new DateTime(2026, 10, 25, 14, 30, 0, DateTimeKind.Utc);

        Assert.Empty(pb.GetBucketStarts(at, at));
        Assert.Empty(pb.GetBucketStarts(at, at.AddMinutes(-10)));
    }

    [Fact]
    public void Parse_rejects_a_trailing_newline()
    {
        var pb = new PathBuilder("raw", TimeGranularity.Day);
        Assert.True(pb.TryParse("raw/2026/10/25/sensor-a.ets", out _, out _));
        Assert.False(pb.TryParse("raw/2026/10/25/sensor-a.ets\n", out _, out _));
    }

    [Fact]
    public void Month_bucket_starts_cross_the_year_boundary()
    {
        var pb = new PathBuilder(TimeGranularity.Month);
        var from = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(3, pb.GetBucketStarts(from, to).Count());
    }

    public static IEnumerable<object[]> ParsePathData => TestCases.Select(x => new object[] { x });

    public static IEnumerable<object[]> AllHoursData => Enumerable.Range(0, 24).Select(h => new object[] { h });

    public static readonly TestCase[] TestCases = {
        TestCase.Ok(TimeGranularity.Day, $"{Root}/2023/12/17/ab01.ets", "2023-12-17", "ab01"),
        TestCase.Fail(TimeGranularity.Day, $"{Root}/2023/12/17/AB01.ets"),
        TestCase.Fail(TimeGranularity.Day, $"{Root.ToUpperInvariant()}/2023/12/17/ab01.ets"),
        TestCase.Fail(TimeGranularity.Day, $"{Root}/2023/12/17/ab01.ETS"),
        TestCase.Fail(TimeGranularity.Day, $"{Root}/2023/12/17/.ab01.ets.0123456789abcdef0123456789abcdef.tmp"),
        TestCase.Ok(TimeGranularity.Day, $"{Root}/2023/12/18/39f9e034c031.ets", "2023-12-18", "39f9e034c031"),
        TestCase.Fail(TimeGranularity.Day, "some/other/root/2023/12/18/39f9e034c031.ets"),
        TestCase.Fail(TimeGranularity.Day, $"{Root}/2023/12/32/ab01.ets"),
        TestCase.Fail(TimeGranularity.Day, $"{Root}/2023/00/17/ab01.ets"),
        TestCase.Fail(TimeGranularity.Day, $"{Root}/0023/12/17/ab01.ets"),

        TestCase.Ok(TimeGranularity.Hour, $"{Root}/2023/06/09/22/80977b4b-6122-4080-87db-a09d93486da1.ets",
            "2023-06-09 22:00", "80977b4b-6122-4080-87db-a09d93486da1"),

        TestCase.Ok(TimeGranularity.Month, $"{Root}/2023/02/123654.ets", "2023-02-01", "123654"),

        TestCase.Ok(TimeGranularity.Year, $"{Root}/2023/a09d93486da1.ets", "2023-01-01", "a09d93486da1"),
    };

    public record TestCase(
        TimeGranularity Granularity,
        string Path,
        bool ShouldSucceed,
        DateTime Time,
        string SubjectId)
    {
        public static TestCase Fail(TimeGranularity granularity, string path)
            => new(granularity, path, false, DateTime.MinValue, string.Empty);

        public static TestCase Ok(TimeGranularity granularity, string path, string timeStr, string subjectId)
            => new(granularity, path, true, DateTime.Parse(timeStr).ForceUtc(), subjectId);
    }

    public class Fixture
    {
        public readonly PathBuilder HourBuilder = new(Root, TimeGranularity.Hour);
        public readonly PathBuilder DayBuilder = new(Root, TimeGranularity.Day);
        public readonly PathBuilder MonthBuilder = new(Root, TimeGranularity.Month);
        public readonly PathBuilder YearBuilder = new(Root, TimeGranularity.Year);

        public PathBuilder BuilderFor(TimeGranularity granularity)
        {
            return granularity switch
            {
                TimeGranularity.Hour => HourBuilder,
                TimeGranularity.Day => DayBuilder,
                TimeGranularity.Month => MonthBuilder,
                TimeGranularity.Year => YearBuilder,
                _ => throw new NotImplementedException(),
            };
        }
    }
}
