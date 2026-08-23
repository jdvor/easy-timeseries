using BenchmarkDotNet.Running;
using ConsoleAppFramework;
using Easy.TimeSeries.Benchmarks;

// First positional argument decides who parses the command line: a known custom command goes to
// ConsoleAppFramework, everything else (including no arguments) goes to BenchmarkDotNet's own CLI.
string[] customCommands = ["size", "list"];
if (args.Length > 0 && customCommands.Contains(args[0], StringComparer.OrdinalIgnoreCase))
{
    var app = ConsoleApp.Create();
    app.Add<SizeCommands>();
    await app.RunAsync(args);
    return;
}

// '--quick' (default) and '--full' are ours, not BenchmarkDotNet's, so they have to be removed from the
// argument list before it is handed over - an unknown switch makes BenchmarkDotNet's parser bail out.
var quick = !args.Contains("--full", StringComparer.OrdinalIgnoreCase);
args = [.. args.Where(a =>
    !a.Equals("--quick", StringComparison.OrdinalIgnoreCase)
    && !a.Equals("--full", StringComparison.OrdinalIgnoreCase))];

var config = new Config(quick: quick);
if (args.Length == 0)
{
    args = ["--filter", "*"];
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
