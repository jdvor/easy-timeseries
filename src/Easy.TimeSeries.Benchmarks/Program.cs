using BenchmarkDotNet.Running;
using Cocona;
using Easy.TimeSeries.Benchmarks;

// First positional argument decides who parses the command line: a known custom command goes to
// Cocona, everything else (including no arguments) goes to BenchmarkDotNet's own CLI.
string[] coconaCommands = ["size", "list"];
if (args.Length > 0 && coconaCommands.Contains(args[0], StringComparer.OrdinalIgnoreCase))
{
    CoconaLiteApp.Run<SizeCommands>(args);
    return;
}

var config = new Config();
if (args.Length == 0)
{
    args = ["--filter", "*"];
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
