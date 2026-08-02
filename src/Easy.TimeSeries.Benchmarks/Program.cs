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

var config = new Config();
if (args.Length == 0)
{
    args = ["--filter", "*"];
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
