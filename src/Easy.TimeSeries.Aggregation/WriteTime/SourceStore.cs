namespace Easy.TimeSeries.Aggregation.WriteTime;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Paths;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Source records: one small JSON file next to each aggregated file, <c>{subject}.sources.json</c> beside
/// <c>{subject}.ets</c>, listing the raw files (path and version) the aggregated file was computed from, and the
/// version of the aggregated file they describe. Recovery compares them with storage, so it needs no clock.
/// </summary>
/// <remarks>
/// <para>
/// A record lives as long as its aggregated file, and deleting the aggregated folder removes both.
/// <see cref="PathBuilder.TryParse"/> of the aggregated layout never matches a record, because subject ids contain
/// no dot. Records are only read by exact path, never listed.
/// </para>
/// <para>
/// A record is written after its aggregated file and names the version that write committed. When two runs overlap,
/// the last record and the last aggregated file can come from different runs; the aggregated version then differs
/// and recovery recomputes. An aggregated file without a record, e.g. one written by the CLI, counts as stale and is
/// recomputed once.
/// </para>
/// </remarks>
internal sealed class SourceStore
{
    private const string Suffix = "sources.json";

    private readonly IStorageCatalog catalog;
    private readonly (AggregationSpec Spec, PathBuilder Paths)[] layouts;

    public SourceStore(IStorageCatalog catalog, AggregationConfiguration configuration)
    {
        this.catalog = catalog;
        layouts =
        [
            .. configuration.Specs.Select(spec => (
                spec,
                new PathBuilder(spec.Root, spec.StorageTimeSpan, Suffix))),
        ];
    }

    public string PathOf(AggregationSpec spec, string subjectId, DateTime spanStart)
    {
        foreach (var (layoutSpec, paths) in layouts)
        {
            if (ReferenceEquals(layoutSpec, spec) || string.Equals(layoutSpec.Root, spec.Root, StringComparison.Ordinal))
            {
                return paths.GetFilePath(spanStart, subjectId);
            }
        }

        throw new ArgumentException($"Spec '{spec.Root}' is not part of the configuration.", nameof(spec));
    }

    /// <summary>Records the raw files an aggregated file was just computed from.</summary>
    /// <param name="aggregated">
    /// Version committed by the aggregated write itself, never one read afterwards; <c>null</c> when no file was
    /// written or the catalog does not report versions.
    /// </param>
    public async Task WriteAsync(
        AggregationSpec spec,
        string subjectId,
        DateTime spanStart,
        IEnumerable<(string Path, StorageVersion Version)> raw,
        StorageVersion? aggregated,
        CancellationToken cancellationToken)
    {
        var record = new SourceRecord(raw.ToDictionary(x => x.Path, x => x.Version.Value, StringComparer.Ordinal))
        {
            Aggregated = aggregated?.Value,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, SourceJson.Default.SourceRecord);
        await using var storage = catalog.OpenWrite(PathOf(spec, subjectId, spanStart));
        await storage.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await storage.CloseAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The record of an aggregated file, or <c>null</c> when it is missing or unreadable.</summary>
    public async Task<SourceRecord?> ReadAsync(
        AggregationSpec spec,
        string subjectId,
        DateTime spanStart,
        CancellationToken cancellationToken)
    {
        var path = PathOf(spec, subjectId, spanStart);
        if (await catalog.GetAsync(path, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        try
        {
            var bytes = await catalog.OpenRead(path).ReadAsync(cancellationToken).ConfigureAwait(false);
            var record = JsonSerializer.Deserialize(bytes.Span, SourceJson.Default.SourceRecord);

            // Valid JSON is not yet a record: a missing raw map deserializes as null.
            return record is { Raw: not null, FormatVersion: 1 } ? record : null;
        }
#pragma warning disable CA1031 // an unreadable record only means the aggregated file is recomputed
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}

/// <summary>Source record content; the layout is internal and versioned by <see cref="FormatVersion"/>.</summary>
/// <param name="Raw">Version of each raw file read, by path.</param>
internal sealed record SourceRecord(Dictionary<string, string> Raw)
{
    public int FormatVersion { get; init; } = 1;

    /// <summary>Version of the aggregated file this record was written for; <c>null</c> when unknown.</summary>
    public string? Aggregated { get; init; }

    /// <summary>
    /// Whether the aggregated file, currently at <paramref name="current"/> (<c>null</c> when missing), is the one
    /// this record was written for. A record without an aggregated version cannot tell and accepts any.
    /// </summary>
    public bool Describes(StorageVersion? current)
        => Aggregated is null || string.Equals(Aggregated, current?.Value, StringComparison.Ordinal);

    /// <summary>Whether the raw file at <paramref name="path"/> was read in exactly this version.</summary>
    public bool Covers(string path, StorageVersion version)
        => Raw.TryGetValue(path, out var recorded) && string.Equals(recorded, version.Value, StringComparison.Ordinal);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SourceRecord))]
internal sealed partial class SourceJson : JsonSerializerContext;
