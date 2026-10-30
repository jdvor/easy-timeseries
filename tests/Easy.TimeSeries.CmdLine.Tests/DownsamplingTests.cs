namespace Easy.TimeSeries.CmdLine.Tests;

using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;
using System.IO;

public sealed class DownsamplingTests : IDisposable
{
    private static readonly DateTime Day = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    private readonly DirectoryInfo root = new(Path.Combine(Path.GetTempPath(), $"ets-cli-{Guid.NewGuid():N}"));
    private readonly List<string> log = [];

    private DirectoryInfo Input => new(Path.Combine(root.FullName, "raw"));

    private DirectoryInfo Output => new(Path.Combine(root.FullName, "out"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_subject_and_span_in_the_range_is_aggregated()
    {
        await WriteRawAsync("sensor-a", Day.AddHours(1), 1d);
        await WriteRawAsync("sensor-a", Day.AddHours(2), 3d);
        await WriteRawAsync("sensor-b", Day.AddDays(1).AddHours(5), 7d);
        await WriteRawAsync("sensor-a", Day.AddDays(5), 9d);

        var summary = await RunAsync();

        Assert.Equal((2, 0, 0, 0), (summary.Written, summary.Empty, summary.Skipped, summary.Failed));
        Assert.Equal(
            ["agg-1h-daily/2026/10/25/sensor-a.ets", "agg-1h-daily/2026/10/26/sensor-b.ets"],
            Directory.GetFiles(Output.FullName, "*.ets", SearchOption.AllDirectories)
                .Select(x => Path.GetRelativePath(Output.FullName, x).Replace('\\', '/'))
                .Order(StringComparer.Ordinal));
        Assert.Contains(log, x => x.StartsWith("agg-1h-daily/2026/10/25/sensor-a.ets: 2 rows", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Subject_option_limits_the_run()
    {
        await WriteRawAsync("sensor-a", Day.AddHours(1), 1d);
        await WriteRawAsync("sensor-b", Day.AddHours(1), 2d);

        var summary = await RunAsync(subject: "sensor-b");

        Assert.Equal(1, summary.Written);
        Assert.True(File.Exists(Path.Combine(Output.FullName, "agg-1h-daily", "2026", "10", "25", "sensor-b.ets")));
    }

    [Fact]
    public async Task Existing_files_are_skipped_unless_forced()
    {
        await WriteRawAsync("sensor-a", Day.AddHours(1), 1d);
        await RunAsync();

        Assert.Equal(1, (await RunAsync()).Skipped);
        Assert.Equal(1, (await RunAsync(force: true)).Written);
    }

    [Fact]
    public async Task Failing_file_is_reported_and_the_run_continues()
    {
        await WriteRawAsync("sensor-a", Day.AddHours(1), 1d);
        await WriteRawAsync("sensor-b", Day.AddHours(1), 1d, label: "Other");

        var summary = await RunAsync();

        Assert.Equal((1, 1), (summary.Written, summary.Failed));
        Assert.Contains(log, x => x.Contains("sensor-b.ets: FAILED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Command_returns_exit_code_one_when_a_file_failed()
    {
        await WriteRawAsync("sensor-a", Day.AddHours(1), 1d, label: "Other");

        var code = await Downsampling.DownsampleAsync(
            Input.FullName, Output.FullName, "hour", "1h", "day", "2026-10-25", "2026-10-26", "V", ct: Ct);

        Assert.Equal(1, code);
    }

    public void Dispose()
    {
        if (root.Exists)
        {
            root.Delete(recursive: true);
        }
    }

    private Task<DownsampleSummary> RunAsync(string? subject = null, bool force = false)
    {
        var options = DownsampleArguments.Parse(
            Input.FullName, Output.FullName, "hour", "1h", "day", "2026-10-25", "2026-10-27", "V=max", subject: subject, force: force);
        return Downsampling.RunAsync(options, log.Add, Ct);
    }

    private async Task WriteRawAsync(string subject, DateTime at, double value, string label = "V")
    {
        var path = new PathBuilder(TimeGranularity.Hour).GetFilePath(at, subject);
        await using var storage = new FileCatalog(Input).OpenWrite(path);
        using var writer = new Writer(1).AddTimeOrdered([at], "t").AddDouble([value], label);
        await writer.WriteToAsync(storage, Ct);
    }
}
