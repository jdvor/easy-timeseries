namespace Easy.TimeSeries.CmdLine;

using ConsoleAppFramework;
using Easy.TimeSeries.Parquet;
using Easy.TimeSeries.Storage;

internal static class Conversions
{
    /// <summary>Convert time-series file to Apache Parquet format.</summary>
    /// <param name="inputPath">-i, Path to the time series file.</param>
    /// <param name="outputPath">-o, Output file path; if not provided, the output will be generated next to the input file.</param>
    /// <param name="force">-f, Overwrite an existing output file and create the output directory if missing.</param>
    public static async Task ConvertToParquetAsync(
        string inputPath,
        string? outputPath = null,
        bool force = false,
        CancellationToken ct = default)
    {
        var input = Path.GetFullPath(inputPath);
        if (!File.Exists(input))
        {
            throw new FileNotFoundException("Input time-series file was not found.", input);
        }

        var output = string.IsNullOrWhiteSpace(outputPath)
            ? Path.ChangeExtension(input, ".parquet")
            : Path.GetFullPath(outputPath);

        var outputDir = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            if (!force)
            {
                throw new DirectoryNotFoundException(
                    $"Output directory '{outputDir}' does not exist. Pass --force to create it.");
            }

            Directory.CreateDirectory(outputDir);
        }

        if (File.Exists(output))
        {
            if (!force)
            {
                throw new IOException(
                    $"Output file '{output}' already exists. Pass --force to overwrite it.");
            }

            File.Delete(output);
        }

        using var source = new FileStorage(input);
        await using var destination = File.Create(output);
        await TsToParquetConverter.ConvertAsync(source, destination, cancellationToken: ct).ConfigureAwait(false);

        ConsoleApp.Log($"{input} -> {output}");
    }
}
