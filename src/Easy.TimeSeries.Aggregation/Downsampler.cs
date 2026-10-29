namespace Easy.TimeSeries.Aggregation;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Aggregation.Engine;
using Easy.TimeSeries.Paths;

/// <summary>
/// Computes aggregated files from raw files in one pass. Raw files are read and decoded one chunk at a time, in time
/// order, so peak memory is about one chunk of the needed columns, independent of the aggregated storage time span.
/// </summary>
/// <remarks>
/// A chunk is one raw file, or the raw files of one aggregation time span when that is longer than the raw storage
/// time span (e.g. <see cref="AggregationTimeSpan.Day"/> over hourly raw files). The instance is immutable and
/// thread-safe; pooled buffers are rented per run.
/// </remarks>
public sealed class Downsampler
{
    private readonly PathBuilder rawPaths;
    private readonly PathBuilder aggregatedPaths;

    /// <exception cref="ArgumentException">The aggregated storage time span is shorter than the raw one.</exception>
    public Downsampler(AggregationSpec spec, TimeGranularity rawStorageTimeSpan)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (rawStorageTimeSpan is < TimeGranularity.Hour or > TimeGranularity.Year)
        {
            throw new ArgumentOutOfRangeException(nameof(rawStorageTimeSpan), rawStorageTimeSpan, "Unknown raw storage time span.");
        }

        if (spec.StorageTimeSpan < rawStorageTimeSpan)
        {
            throw new ArgumentException(
                $"Aggregated storage time span {spec.StorageTimeSpan} is shorter than the raw storage time span {rawStorageTimeSpan}.",
                nameof(rawStorageTimeSpan));
        }

        Spec = spec;
        RawStorageTimeSpan = rawStorageTimeSpan;
        rawPaths = new PathBuilder(rawStorageTimeSpan);
        aggregatedPaths = spec.CreatePathBuilder();
    }

    public AggregationSpec Spec { get; }

    public TimeGranularity RawStorageTimeSpan { get; }

    /// <summary>Aggregates raw storages into one aggregated file written to <paramref name="target"/>.</summary>
    /// <param name="storageSpanStart">Start of the aggregated storage time span; each raw bucket must lie inside it.</param>
    /// <param name="rawFiles">Raw files in any order; they are read in bucket order.</param>
    /// <param name="target">
    /// Destination of the aggregated file. When the result has no rows, it is never written to and
    /// <c>CloseAsync</c> is not called, so no file appears.
    /// </param>
    /// <exception cref="AggregationException">A raw file does not match the spec, or a result cannot be stored.</exception>
    public async Task<AggregationResult> RunAsync(
        DateTime storageSpanStart,
        IReadOnlyList<RawFile> rawFiles,
        IWriteStorage target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawFiles);
        ArgumentNullException.ThrowIfNull(target);
        var spanEnd = ValidateSpanStart(storageSpanStart);
        foreach (var file in rawFiles)
        {
            ValidateRawFile(file, storageSpanStart, spanEnd);
        }

        using var run = new AggregationRun(Spec, rawPaths, storageSpanStart);
        return await run.ExecuteAsync(rawFiles, target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Same over a single raw buffer already in memory.</summary>
    public Task<AggregationResult> RunAsync(
        DateTime storageSpanStart,
        RawBuffer rawBuffer,
        IWriteStorage target,
        CancellationToken cancellationToken = default)
        => RunAsync(
            storageSpanStart,
            [new RawFile(rawBuffer.BucketStart, new MemoryReadStorage(rawBuffer.Buffer), rawBuffer.Name)],
            target,
            cancellationToken);

    /// <summary>
    /// Finds the subject's raw files of the storage time span in <paramref name="catalog"/> and writes the aggregated
    /// file to the spec's path, <c>{spec root}/{date folders}/{subjectId}.ets</c>.
    /// </summary>
    public async Task<AggregationResult> RunAsync(
        IStorageCatalog catalog,
        PathBuilder raw,
        string subjectId,
        DateTime storageSpanStart,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Granularity != RawStorageTimeSpan)
        {
            throw new ArgumentException(
                $"Raw path builder partitions by {raw.Granularity}, but the downsampler expects {RawStorageTimeSpan}.",
                nameof(raw));
        }

        var spanEnd = ValidateSpanStart(storageSpanStart);
        var targetPath = aggregatedPaths.GetFilePath(storageSpanStart, subjectId);
        var (_, prefix) = raw.GetExpectedPaths(storageSpanStart, spanEnd);
        var files = new List<RawFile>();
        await foreach (var entry in catalog.ListAsync(prefix, cancellationToken).ConfigureAwait(false))
        {
            if (raw.TryParse(entry.Path, out var bucketStart, out var subject)
                && string.Equals(subject, subjectId, StringComparison.Ordinal)
                && bucketStart >= storageSpanStart
                && bucketStart < spanEnd)
            {
                files.Add(new RawFile(bucketStart, catalog.OpenRead(entry.Path), entry.Path));
            }
        }

        await using var target = catalog.OpenWrite(targetPath);
        return await RunAsync(storageSpanStart, files, target, cancellationToken).ConfigureAwait(false);
    }

    private DateTime ValidateSpanStart(DateTime storageSpanStart)
    {
        if (storageSpanStart.Kind != DateTimeKind.Utc || aggregatedPaths.GetBucketStart(storageSpanStart) != storageSpanStart)
        {
            throw new ArgumentException(
                $"{storageSpanStart:o} is not the UTC start of a {Spec.StorageTimeSpan} storage time span.",
                nameof(storageSpanStart));
        }

        return aggregatedPaths.GetBucketEnd(storageSpanStart);
    }

    private void ValidateRawFile(RawFile file, DateTime spanStart, DateTime spanEnd)
    {
        ArgumentNullException.ThrowIfNull(file.Storage, nameof(RawFile.Storage));
        var bucket = file.BucketStart;
        if (bucket.Kind != DateTimeKind.Utc || rawPaths.GetBucketStart(bucket) != bucket || bucket < spanStart || bucket >= spanEnd)
        {
            throw new ArgumentException(
                $"Raw file '{file.Name ?? file.Storage.ToString()}' starts at {bucket:o}, which is not the UTC start of a "
                + $"{RawStorageTimeSpan} bucket within [{spanStart:o}, {spanEnd:o}).",
                nameof(file));
        }
    }

    private sealed class MemoryReadStorage(ReadOnlyMemory<byte> buffer) : IReadStorage
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(buffer);
    }
}
