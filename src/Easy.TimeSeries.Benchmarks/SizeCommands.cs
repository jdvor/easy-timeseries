namespace Easy.TimeSeries.Benchmarks;

using ConsoleAppFramework;
using Easy.TimeSeries.Benchmarks.Size;

public sealed class SizeCommands
{
    /// <summary>Compare serialized size of test datasets: CSV vs Parquet (snappy) vs Easy.TimeSeries.</summary>
    /// <param name="datasets">Dataset names to measure; all datasets when omitted. See the 'list' command.</param>
    /// <param name="output">-o, Path of the markdown report file.</param>
    [Command("size")]
    public async Task<int> SizeAsync(
        [Argument] string[]? datasets = null,
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

    /// <summary>List datasets available for the size comparison.</summary>
    [Command("list")]
    public void List()
    {
        foreach (var name in DatasetSizer.Names)
        {
            Console.WriteLine(name);
        }
    }
}
