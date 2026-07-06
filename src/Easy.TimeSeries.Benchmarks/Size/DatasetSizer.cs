namespace Easy.TimeSeries.Benchmarks.Size;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Storage;
using Easy.TimeSeries.TestData;
using Parquet.Serialization;

/// <summary>
/// Measures serialized sizes of the well-known test datasets. One pass per format, no repetition -
/// sizes are deterministic, so this deliberately bypasses BenchmarkDotNet.
/// </summary>
public static class DatasetSizer
{
    private static readonly (string Name, Func<Task<SizeResult>> Measure)[] Sizers =
    [
        ("powerplants", () => MeasureAsync("powerplants", Data.GetPowerPlants(), Data.GetPowerPlantsCsvFileSize(), new PowerPlantWriter())),
        ("boeing", () => MeasureAsync("boeing", Data.GetBoeing(), Data.GetBoeingCsvFileSize(), new BoeingWriter())),
        ("macro4", () => MeasureAsync("macro4", Data.GetMacro4(), Data.GetMacro4CsvFileSize(), new Macro4Writer())),
        ("gold", () => MeasureAsync("gold", Data.GetGold(), Data.GetGoldCsvFileSize(), new GoldWriter())),
        ("vix", () => MeasureAsync("vix", Data.GetVix(), Data.GetVixCsvFileSize(), new VixWriter())),
    ];

    public static IReadOnlyList<string> Names { get; } = [.. Sizers.Select(x => x.Name)];

    public static bool TryGet(string name, out Func<Task<SizeResult>> measure)
    {
        foreach (var (n, m) in Sizers)
        {
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            {
                measure = m;
                return true;
            }
        }

        measure = null!;
        return false;
    }

    private static async Task<SizeResult> MeasureAsync<T>(string name, List<T> rows, long csvBytes, IWriter<T> etsWriter)
        where T : class
    {
        long parquetBytes;
        using (var stream = new MemoryStream())
        {
            await ParquetSerializer.SerializeAsync(rows, stream).ConfigureAwait(false);
            parquetBytes = stream.Length;
        }

        using var storage = new InMemoryStorage();
        await etsWriter.WriteAsync(rows, storage).ConfigureAwait(false);

        return new SizeResult(name, rows.Count, csvBytes, parquetBytes, storage.Size);
    }
}
