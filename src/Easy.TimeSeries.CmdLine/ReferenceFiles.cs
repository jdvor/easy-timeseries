namespace Easy.TimeSeries.CmdLine;

using ConsoleAppFramework;
using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Parquet;
using Easy.TimeSeries.Storage;
using Easy.TimeSeries.TestData;

/// <summary>
/// Generates a fixed set of reference files from the bundled test datasets. The output is a corpus of golden files
/// (one per dataset) used for regression and cross-version compatibility testing of the on-disk formats.
/// </summary>
internal static class ReferenceFiles
{
    /// <summary>Generate reference Easy.TimeSeries (.ts) files into a directory.</summary>
    /// <param name="dirPath">-d, Target directory for the generated files.</param>
    public static async Task GenerateTimeSeriesAsync(
        string dirPath,
        CancellationToken ct = default)
    {
        var dir = EnsureDirectory(dirPath);

        await WriteTsAsync(new PowerPlantWriter(), Data.GetPowerPlants(), dir, "power_plants", ct).ConfigureAwait(false);
        await WriteTsAsync(new BoeingWriter(), Data.GetBoeing(), dir, "Boeing", ct).ConfigureAwait(false);
        await WriteTsAsync(new Macro4Writer(), Data.GetMacro4(), dir, "Macro4Series", ct).ConfigureAwait(false);
        await WriteTsAsync(new GoldWriter(), Data.GetGold(), dir, "Gold", ct).ConfigureAwait(false);
        await WriteTsAsync(new VixWriter(), Data.GetVix(), dir, "d-vix0411", ct).ConfigureAwait(false);
    }

    /// <summary>Generate reference Apache Parquet files into a directory.</summary>
    /// <param name="dirPath">-d, Target directory for the generated files.</param>
    public static async Task GenerateParquetAsync(
        string dirPath,
        CancellationToken ct = default)
    {
        var dir = EnsureDirectory(dirPath);

        await WriteParquetAsync(new PowerPlantWriter(), Data.GetPowerPlants(), dir, "power_plants", ct).ConfigureAwait(false);
        await WriteParquetAsync(new BoeingWriter(), Data.GetBoeing(), dir, "Boeing", ct).ConfigureAwait(false);
        await WriteParquetAsync(new Macro4Writer(), Data.GetMacro4(), dir, "Macro4Series", ct).ConfigureAwait(false);
        await WriteParquetAsync(new GoldWriter(), Data.GetGold(), dir, "Gold", ct).ConfigureAwait(false);
        await WriteParquetAsync(new VixWriter(), Data.GetVix(), dir, "d-vix0411", ct).ConfigureAwait(false);
    }

    private static async Task WriteTsAsync<T>(
        IWriter<T> writer,
        List<T> data,
        string dir,
        string name,
        CancellationToken ct)
        where T : class
    {
        var path = Path.Combine(dir, name + ".ts");
        DeleteIfExists(path);

        using var storage = new FileStorage(path);
        await writer.WriteAsync(data, storage, ct).ConfigureAwait(false);

        ConsoleApp.Log($"{data.Count,8} rows -> {path}");
    }

    private static async Task WriteParquetAsync<T>(
        IWriter<T> writer,
        List<T> data,
        string dir,
        string name,
        CancellationToken ct)
        where T : class
    {
        // Encode to the columnar buffer in memory, then convert that buffer to Parquet - the converter reuses the
        // core reader and is driven entirely by the self-describing header, so no schema/DTO is needed here.
        using var storage = new InMemoryStorage();
        await writer.WriteAsync(data, storage, ct).ConfigureAwait(false);

        var path = Path.Combine(dir, name + ".parquet");
        DeleteIfExists(path);

        await using var destination = File.Create(path);
        await TsToParquetConverter.ConvertAsync(storage.WrittenMemory, destination, cancellationToken: ct).ConfigureAwait(false);

        ConsoleApp.Log($"{data.Count,8} rows -> {path}");
    }

    private static string EnsureDirectory(string dirPath)
    {
        var full = Path.GetFullPath(dirPath);
        Directory.CreateDirectory(full);
        return full;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
