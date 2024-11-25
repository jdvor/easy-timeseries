using BenchmarkDotNet.Running;
using Easy.TimeSeries.Benchmarks;

if (args.Length > 0)
{
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    return;
}

BenchmarkRunner.Run<MySandbox>();
