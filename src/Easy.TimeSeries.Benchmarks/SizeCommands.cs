namespace Easy.TimeSeries.Benchmarks;

using Cocona;
using Easy.TimeSeries.Benchmarks.Size;

public sealed class SizeCommands
{
    [Command("size", Description = "Compare serialized size of test datasets: CSV vs Parquet (snappy) vs Easy.TimeSeries.")]
    public async Task<int> SizeAsync(
        [Argument(Description = "Dataset names to measure; all datasets when omitted. See the 'list' command.")]
        string[]? datasets = null,
        [Option('o', Description = "Path of the markdown report file.")]
        string output = "artifacts/size-report.md")
    {
        var names = datasets is { Length: > 0 }
            ? datasets
            : [.. DatasetSizer.Names];

        var results = new List<SizeResult>(names.Length);
        foreach (var name in names)
        {
            if (!DatasetSizer.TryGet(name, out var measure))
            {
                await Console.Error.WriteLineAsync(
                    $"Unknown dataset '{name}'. Available: {string.Join(", ", DatasetSizer.Names)}").ConfigureAwait(false);
                return 1;
            }

            results.Add(await measure().ConfigureAwait(false));
        }

        var markdown = SizeReport.ToMarkdown(results);
        Console.WriteLine(markdown);

        var dir = Path.GetDirectoryName(Path.GetFullPath(output));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(output, markdown).ConfigureAwait(false);
        Console.WriteLine($"Report written to {Path.GetFullPath(output)}");

        return 0;
    }

    [Command("list", Description = "List datasets available for the size comparison.")]
    public void List()
    {
        foreach (var name in DatasetSizer.Names)
        {
            Console.WriteLine(name);
        }
    }
}
