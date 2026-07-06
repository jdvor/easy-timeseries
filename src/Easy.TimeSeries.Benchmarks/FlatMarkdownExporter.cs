namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

public sealed class FlatMarkdownExporter : IExporter
{
    private static readonly IExporter Inner = MarkdownExporter.GitHub;

    public string Name => Inner.Name;

    public void ExportToLog(Summary summary, ILogger logger) => Inner.ExportToLog(summary, logger);

    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        var artifactsPath = Directory.GetParent(summary.ResultsDirectoryPath)?.FullName ?? summary.ResultsDirectoryPath;
        var filePath = Path.Combine(artifactsPath, $"{summary.Title}.md");

        using (var writer = new StreamWriter(filePath, append: false))
        {
            Inner.ExportToLog(summary, new StreamLogger(writer));
        }

        TryRemoveEmptyResultsDir(summary.ResultsDirectoryPath);

        return [filePath];
    }

    private static void TryRemoveEmptyResultsDir(string resultsDirectoryPath)
    {
        try
        {
            if (Directory.Exists(resultsDirectoryPath) &&
                !Directory.EnumerateFileSystemEntries(resultsDirectoryPath).Any())
            {
                Directory.Delete(resultsDirectoryPath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; leaving an empty directory is harmless.
        }
    }
}
