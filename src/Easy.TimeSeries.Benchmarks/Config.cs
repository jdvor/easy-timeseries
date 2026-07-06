namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using Easy.TimeSeries.TestData;
using System.Globalization;

public class Config : ManualConfig
{
    public Config(string? outputPath = null)
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

        // Trim the default job (adaptive warmup + 15 measured iterations) down to a faster, still-usable run.
        // Loosen back up (or drop this job) when a result needs to be pinned precisely.
        AddJob(Job.Default
            .WithLaunchCount(1)
            .WithWarmupCount(3)
            .WithIterationCount(5));
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
