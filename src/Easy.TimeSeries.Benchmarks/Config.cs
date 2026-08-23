namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using Easy.TimeSeries.TestData;
using System.Globalization;

public class Config : ManualConfig
{
    /// <param name="outputPath">Artifacts directory; a dated sub-directory of 'artifacts/' when omitted.</param>
    /// <param name="quick">
    /// When true (default), trims the job down to a faster run with less statistical confidence.
    /// Pass false for the standard BenchmarkDotNet job when a result needs to be pinned precisely.
    /// </param>
    public Config(string? outputPath = null, bool quick = true)
    {
        var path = GetAndEnsureOutputPath(outputPath);

        WithOptions(ConfigOptions.JoinSummary
                    | ConfigOptions.KeepBenchmarkFiles
                    | ConfigOptions.StopOnFirstError
                    | ConfigOptions.DisableLogFile);
        ArtifactsPath = path;
        CultureInfo = CultureInfo.InvariantCulture;

        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddExporter(new FlatMarkdownExporter());

        // Quick mode trims the default job (adaptive warmup + 15 measured iterations) down to a faster,
        // still-usable run; standard mode keeps BenchmarkDotNet's own defaults.
        AddJob(quick
            ? Job.Default
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(5)
            : Job.Default);
    }

    private static string GetAndEnsureOutputPath(string? outputPath = null)
    {
        string path;
        if (!string.IsNullOrEmpty(outputPath))
        {
            path = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(path);
            return path;
        }

        var slnDir = Data.FindSolutionDirPath();
        var subDir = TimeProvider.System.GetLocalNow().ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture);
        path = Path.Combine(slnDir, "artifacts", subDir);
        Directory.CreateDirectory(path);
        return path;
    }
}
