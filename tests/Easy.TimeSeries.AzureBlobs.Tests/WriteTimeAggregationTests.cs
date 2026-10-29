namespace Easy.TimeSeries.AzureBlobs.Tests;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Aggregation;
using Easy.TimeSeries.Paths;

/// <summary>
/// Write-time aggregation over a real blob container: markers are blobs, and the processor's conditional delete is
/// an ETag precondition checked by the service.
/// </summary>
[Collection(nameof(FixtureCollection))]
public class WriteTimeAggregationTests
{
    private static readonly DateTime Day = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    private readonly AzureBlobCatalog blobs;
    private readonly Clock clock = new() { Now = Day.AddHours(1) };

    public WriteTimeAggregationTests(Fixture fixture)
    {
        blobs = new AzureBlobCatalog(fixture.CreateContainer());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Raw_writes_aggregate_through_blob_markers_with_etag_preconditions()
    {
        var interleaving = new InterleavingCatalog(blobs);
        var configuration = new AggregationConfiguration
        {
            Raw = new PathBuilder("raw", TimeGranularity.Hour),
            Specs =
            [
                new AggregationSpec(AggregationTimeSpan.Minute, TimeGranularity.Hour).Column("V", Agg.Avg),
                new AggregationSpec(AggregationTimeSpan.Hour, TimeGranularity.Day).Column("V", Agg.Max),
            ],
            RawRetention = TimeSpan.FromDays(30),
            TimeProvider = clock,
        };
        var catalog = new AggregatingCatalog(blobs, configuration);
        var maintenance = new AggregationMaintenance(interleaving, configuration);

        await WriteRawAsync(catalog, 0, 1d);
        Assert.NotNull(await blobs.GetAsync("agg-1m-hourly/2026/10/25/00/sensor-a.ets", Ct));
        Assert.NotNull(await blobs.GetAsync("dirty/agg-1h-daily/2026/10/25/sensor-a.json", Ct));

        // A raw write lands while the daily file is recomputed: the marker's ETag changes and the delete must fail.
        clock.Now = Day.AddDays(2);
        interleaving.OnAggregatedWrite = () => WriteRawAsync(catalog, 5, 9d);
        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Processed);
        Assert.NotNull(await blobs.GetAsync("dirty/agg-1h-daily/2026/10/25/sensor-a.json", Ct));

        Assert.Equal(1, (await maintenance.ProcessDirtyAsync(Ct)).Processed);
        Assert.Null(await blobs.GetAsync("dirty/agg-1h-daily/2026/10/25/sensor-a.json", Ct));

        var daily = new Columns();
        await Reader.ReadFromAsync(blobs.OpenRead("agg-1h-daily/2026/10/25/sensor-a.ets"), daily, null, Ct);
        Assert.Equal([1d, 9d], daily.Values[2]);
    }

    private static async Task WriteRawAsync(AggregatingCatalog catalog, int hour, double value)
    {
        var at = Day.AddHours(hour);
        await using var storage = catalog.OpenRawWrite(at, "sensor-a");
        using var writer = new Writer(1).AddTimeOrdered([at], "t").AddDouble([value], "V");
        await writer.WriteToAsync(storage, Ct);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Runs a callback once, right before the first aggregated file is opened for writing.</summary>
    private sealed class InterleavingCatalog(IStorageCatalog inner) : IStorageCatalog
    {
        public Func<Task>? OnAggregatedWrite { get; set; }

        public IAsyncEnumerable<StorageEntry> ListAsync(string prefix, CancellationToken cancellationToken = default)
            => inner.ListAsync(prefix, cancellationToken);

        public Task<StorageEntry?> GetAsync(string path, CancellationToken cancellationToken = default)
            => inner.GetAsync(path, cancellationToken);

        public IReadStorage OpenRead(string path) => inner.OpenRead(path);

        public IWriteStorage OpenWrite(string path, StorageVersion? ifMatch = null)
        {
            if (OnAggregatedWrite is { } callback && path.StartsWith("agg-", StringComparison.Ordinal))
            {
                OnAggregatedWrite = null;
                callback().GetAwaiter().GetResult();
            }

            return inner.OpenWrite(path, ifMatch);
        }

        public Task<bool> DeleteAsync(string path, StorageVersion? ifMatch = null, CancellationToken cancellationToken = default)
            => inner.DeleteAsync(path, ifMatch, cancellationToken);
    }

    private sealed class Columns : IMaterializer<Columns.Row>
    {
        private List<object>? current;

        public Dictionary<int, List<object>> Values { get; } = [];

        public bool BeginColumn(int column, int rowCount)
        {
            current = Values[column] = [];
            return true;
        }

        public void Hydrate<TPropValue>(TPropValue value)
            where TPropValue : struct
            => current!.Add(value);

        public void Hydrate(string value) => current!.Add(value);

        public Row[] GetResult() => [];

        internal sealed class Row;
    }
}
