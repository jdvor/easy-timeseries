namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Configs;
using System.Globalization;

public class Config : ManualConfig
{
    public Config()
    {
        WithOptions(ConfigOptions.JoinSummary | ConfigOptions.KeepBenchmarkFiles | ConfigOptions.StopOnFirstError);
        ArtifactsPath = "artifacts";
        CultureInfo = CultureInfo.InvariantCulture;
    }
}
