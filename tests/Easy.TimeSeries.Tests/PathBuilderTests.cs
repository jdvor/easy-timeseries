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
        Assert.Equal("2026/08/02/20", path);
    }

    public static IEnumerable<object[]> ParsePathData => TestCases.Select(x => new object[] { x });

    public static readonly TestCase[] TestCases = {
        TestCase.Ok(TimeGranularity.Day, $"{Root}/2023/12/17/AB01.ts", "2023-12-17", "AB01"),
        TestCase.Ok(TimeGranularity.Day, $"{Root}/2023/12/18/39f9e034c031.ts", "2023-12-18", "39f9e034c031"),
        TestCase.Fail(TimeGranularity.Day, "some/other/root/2023/12/18/39f9e034c031.ts"),
        TestCase.Fail(TimeGranularity.Day, $"{Root}/2023/12/32/AB01.ts"),
        TestCase.Fail(TimeGranularity.Day, $"{Root}/2023/00/17/AB01.ts"),
        TestCase.Fail(TimeGranularity.Day, $"{Root}/0023/12/17/AB01.ts"),

        TestCase.Ok(TimeGranularity.Hour, $"{Root}/2023/06/09/22/80977b4b-6122-4080-87db-a09d93486da1.ts",
            "2023-06-09 22:00", "80977b4b-6122-4080-87db-a09d93486da1"),

        TestCase.Ok(TimeGranularity.Month, $"{Root}/2023/02/123654.ts", "2023-02-01", "123654"),

        TestCase.Ok(TimeGranularity.Year, $"{Root}/2023/a09d93486da1.ts", "2023-01-01", "a09d93486da1"),
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
