namespace Easy.TimeSeries.Aggregation;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Aggregation.WriteTime;
using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;
using System.Runtime.CompilerServices;

/// <summary>
/// Background work of write-time aggregation: recomputes aggregated files whose dirty marker is set once their span
/// has closed, and finds stale aggregated files the markers missed. The library starts no threads; call these
/// methods from a scheduled job or a hosted service, possibly in another process than the raw writers.
/// </summary>
/// <remarks>
/// On Azure, conditional deletes use ETags, so several processors may run at once. On local files the
/// compare-and-delete is not atomic across processes: run one processor per root.
/// </remarks>
public sealed class AggregationMaintenance
{
    private readonly IStorageCatalog catalog;
    private readonly AggregationConfiguration configuration;
    private readonly MarkerStore markers;
    private readonly SourceStore sources;
    private readonly Dictionary<AggregationSpec, (PathBuilder Paths, Downsampler Downsampler)> specs;

    /// <param name="catalog">The plain catalog, not an <see cref="AggregatingCatalog"/>: maintenance writes no raw files.</param>
    /// <exception cref="ArgumentException">The configuration is inconsistent.</exception>
    public AggregationMaintenance(IStorageCatalog catalog, AggregationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(configuration);
        this.catalog = catalog;
        this.configuration = configuration.Validate();
        markers = new MarkerStore(catalog, configuration);
        sources = new SourceStore(catalog, configuration);
        specs = configuration.Specs.ToDictionary(
            spec => spec,
            spec => (spec.CreatePathBuilder(), new Downsampler(spec, configuration.Raw.Granularity)));
    }

    /// <summary>Recomputes aggregated files whose marker is set and whose storage time span is closed.</summary>
    public async Task<MarkerRunResult> ProcessDirtyAsync(CancellationToken cancellationToken = default)
    {
        var pending = new List<Marker>();
        await foreach (var marker in markers.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            pending.Add(marker);
        }

        // Markers of one storage time span share one raw listing; processing them together releases it early.
        var listings = pending
            .GroupBy(m => (m.Spec.StorageTimeSpan, m.SpanStart))
            .ToDictionary(g => g.Key, g => new SpanListing(
                catalog,
                configuration.Raw,
                g.Key.SpanStart,
                Configured(g.First().Spec).Paths.GetBucketEnd(g.Key.SpanStart),
                g.Select(m => m.SubjectId).ToHashSet(StringComparer.Ordinal),
                g.Count(),
                cancellationToken));
        pending.Sort((a, b) => (a.SpanStart, a.Spec.StorageTimeSpan).CompareTo((b.SpanStart, b.Spec.StorageTimeSpan)));

        var counts = new int[6];
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = configuration.MaxDegreeOfParallelism,
            CancellationToken = cancellationToken,
        };
        await Parallel.ForEachAsync(pending, options, async (marker, ct) =>
        {
            var listing = listings[(marker.Spec.StorageTimeSpan, marker.SpanStart)];
            try
            {
                var outcome = await ProcessAsync(marker, listing, ct).ConfigureAwait(false);
                Interlocked.Increment(ref counts[(int)outcome]);
            }
            finally
            {
                listing.Release();
            }
        }).ConfigureAwait(false);

        return new MarkerRunResult(
            Processed: counts[(int)Outcome.Processed],
            SkippedOpen: counts[(int)Outcome.SkippedOpen],
            Retried: counts[(int)Outcome.Retried],
            MovedToFailed: counts[(int)Outcome.MovedToFailed],
            RejectedByRetention: counts[(int)Outcome.RejectedByRetention],
            Deferred: counts[(int)Outcome.Deferred]);
    }

    /// <summary>
    /// Sets markers for stale aggregated files of raw buckets in the range, and removes orphaned temp files of a
    /// <see cref="FileCatalog"/>. Recomputes nothing itself; <see cref="ProcessDirtyAsync"/> does.
    /// </summary>
    /// <remarks>
    /// An aggregated file is stale when its source record is missing, when a raw file in the range is not in the
    /// record in its current version, or when the record was written for another version of the aggregated file. Deleted raw files never make it stale. The range is clipped to the raw
    /// retention; spans that may have lost raw files are reported, never marked. A span with a failed marker is marked
    /// again only when one of its raw files is newer than the failure.
    /// </remarks>
    public async Task<RecoveryResult> RecoverAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        if (fromUtcInclusive.Kind != DateTimeKind.Utc || toUtcExclusive.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Both bounds must be UTC.");
        }

        var raw = configuration.Raw;
        var now = configuration.TimeProvider.GetUtcNow().UtcDateTime;
        if (configuration.RawRetention != Timeout.InfiniteTimeSpan && fromUtcInclusive < now - configuration.RawRetention)
        {
            fromUtcInclusive = now - configuration.RawRetention;
        }

        var scanned = 0;
        var marked = 0;
        if (fromUtcInclusive < toUtcExclusive)
        {
            var dirty = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var marker in markers.ListAsync(cancellationToken).ConfigureAwait(false))
            {
                dirty.Add(marker.Entry.Path);
            }

            var failed = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
            await foreach (var marker in markers.ListFailedAsync(cancellationToken).ConfigureAwait(false))
            {
                failed[marker.Entry.Path] = marker.Entry.LastModified;
            }

            var from = raw.GetBucketStart(fromUtcInclusive);
            var last = raw.GetBucketStart(toUtcExclusive);
            var to = last == toUtcExclusive ? last : raw.GetBucketEnd(last);
            var spans = new Dictionary<(AggregationSpec Spec, string Subject, DateTime SpanStart), RawInSpan>();
            var (_, prefix) = raw.GetExpectedPaths(from, to);
            await foreach (var entry in catalog.ListAsync(prefix, cancellationToken).ConfigureAwait(false))
            {
                if (!raw.TryParse(entry.Path, out var bucketStart, out var subjectId) || bucketStart < from || bucketStart >= to)
                {
                    continue;
                }

                ++scanned;
                foreach (var (spec, (paths, _)) in specs)
                {
                    var key = (spec, subjectId, paths.GetBucketStart(bucketStart));
                    if (!spans.TryGetValue(key, out var span))
                    {
                        spans[key] = span = new RawInSpan();
                    }

                    span.Add(entry);
                }
            }

            foreach (var ((spec, subjectId, spanStart), span) in spans)
            {
                // A failed span stays failed until a raw file newer than its failure arrives, like a hooked raw write.
                if (dirty.Contains(markers.PathOf(spec, subjectId, spanStart))
                    || (failed.TryGetValue(markers.FailedPathOf(spec, subjectId, spanStart), out var failedAt) && span.Newest <= failedAt)
                    || !await IsStaleAsync(spec, subjectId, spanStart, span.Files, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                if (configuration.IsBeyondRetention(spanStart))
                {
                    configuration.Report(new AggregationFailure(
                        AggregationFailureKind.RejectedByRetention, spec, subjectId, spanStart, null, 0, null));
                    continue;
                }

                await markers.SetAsync(spec, subjectId, spanStart, cancellationToken).ConfigureAwait(false);
                ++marked;
            }
        }

        var removed = catalog is FileCatalog files
            ? await files.RemoveOrphanedTempFilesAsync(configuration.OrphanedTempFileAge, configuration.TimeProvider, cancellationToken)
                .ConfigureAwait(false)
            : 0;
        return new RecoveryResult(scanned, marked, removed);
    }

    /// <summary>Markers that failed <see cref="AggregationConfiguration.MaxAttempts"/> times, with their last error.</summary>
    public async IAsyncEnumerable<FailedMarker> ListFailedAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var marker in markers.ListFailedAsync(cancellationToken).ConfigureAwait(false))
        {
            var content = await markers.ReadAsync(marker.Entry.Path, cancellationToken).ConfigureAwait(false);
            yield return new FailedMarker(
                marker.Spec,
                marker.SubjectId,
                marker.SpanStart,
                content.Attempts,
                content.LastError ?? string.Empty,
                content.UpdatedAt);
        }
    }

    /// <summary>Moves a failed marker back with a zero attempt count, e.g. after fixing a spec or a raw file.</summary>
    public async Task RearmAsync(FailedMarker marker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(marker);
        await MarkDirtyAsync(marker.Spec, marker.SubjectId, marker.StorageSpanStart, cancellationToken).ConfigureAwait(false);
        await catalog.DeleteAsync(markers.FailedPathOf(marker.Spec, marker.SubjectId, marker.StorageSpanStart), null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Requests a recompute of one aggregated file, e.g. after the spec changed.</summary>
    /// <exception cref="ArgumentException">The spec is not configured, or the span start is not aligned.</exception>
    public Task MarkDirtyAsync(
        AggregationSpec spec,
        string subjectId,
        DateTime storageSpanStart,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var (paths, _) = Configured(spec);
        if (storageSpanStart.Kind != DateTimeKind.Utc || paths.GetBucketStart(storageSpanStart) != storageSpanStart)
        {
            throw new ArgumentException(
                $"{storageSpanStart:o} is not the UTC start of a {spec.StorageTimeSpan} storage time span.",
                nameof(storageSpanStart));
        }

        return markers.SetAsync(spec, subjectId, storageSpanStart, cancellationToken);
    }

    private (PathBuilder Paths, Downsampler Downsampler) Configured(AggregationSpec spec)
        => specs.TryGetValue(spec, out var entry)
            ? entry
            : specs.FirstOrDefault(x => string.Equals(x.Key.Root, spec.Root, StringComparison.Ordinal)).Value is { Paths: not null } byRoot
                ? byRoot
                : throw new ArgumentException($"Spec '{spec.Root}' is not part of the configuration.", nameof(spec));

    private async Task<bool> IsStaleAsync(
        AggregationSpec spec,
        string subjectId,
        DateTime spanStart,
        List<StorageEntry> rawFiles,
        CancellationToken cancellationToken)
    {
        var record = await sources.ReadAsync(spec, subjectId, spanStart, cancellationToken).ConfigureAwait(false);
        if (record is null || rawFiles.Exists(raw => !record.Covers(raw.Path, raw.Version)))
        {
            return true;
        }

        // The record must describe the aggregated file now in storage, not one an overlapping run wrote over it.
        var aggregated = await catalog.GetAsync(Configured(spec).Paths.GetFilePath(spanStart, subjectId), cancellationToken)
            .ConfigureAwait(false);
        return !record.Describes(aggregated?.Version);
    }

    /// <summary>Raw files of one aggregated file found by recovery.</summary>
    private sealed class RawInSpan
    {
        public List<StorageEntry> Files { get; } = [];

        public DateTimeOffset Newest { get; private set; } = DateTimeOffset.MinValue;

        public void Add(StorageEntry entry)
        {
            Files.Add(entry);
            if (entry.LastModified > Newest)
            {
                Newest = entry.LastModified;
            }
        }
    }

    private enum Outcome
    {
        Processed,
        SkippedOpen,
        Retried,
        MovedToFailed,
        RejectedByRetention,
        Deferred,
    }

    private async Task<Outcome> ProcessAsync(Marker marker, SpanListing listing, CancellationToken cancellationToken)
    {
        var (paths, downsampler) = Configured(marker.Spec);
        var spanEnd = paths.GetBucketEnd(marker.SpanStart);
        if (configuration.TimeProvider.GetUtcNow().UtcDateTime < spanEnd + configuration.GracePeriod)
        {
            return Outcome.SkippedOpen;
        }

        var path = marker.Entry.Path;
        var version = marker.Entry.Version;
        if (configuration.IsBeyondRetention(marker.SpanStart))
        {
            await catalog.DeleteAsync(path, version, cancellationToken).ConfigureAwait(false);
            Report(AggregationFailureKind.RejectedByRetention, marker, 0, null);
            return Outcome.RejectedByRetention;
        }

        // A raw write resets the attempt count, so fresh data is never held back by an earlier failure.
        var content = await markers.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (content.Attempts > 0
            && configuration.TimeProvider.GetUtcNow() < content.UpdatedAt + configuration.RetryDelayAfter(content.Attempts))
        {
            return Outcome.Deferred;
        }

        try
        {
            var files = await listing.FilesOfAsync(marker.SubjectId).ConfigureAwait(false);
            var (result, aggregatedVersion) = await downsampler
                .RunAsync(catalog, marker.SubjectId, marker.SpanStart, files, cancellationToken)
                .ConfigureAwait(false);

            // Versions as listed: a raw file changed after its listing does not match, so recovery recomputes once more.
            if (result.Written)
            {
                await sources.WriteAsync(
                        marker.Spec,
                        marker.SubjectId,
                        marker.SpanStart,
                        files.Select(x => (x.Path, x.Version)),
                        aggregatedVersion,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return await FailAsync(marker, content.Attempts + 1, ex, cancellationToken).ConfigureAwait(false);
        }

        // A raw write during the recompute rewrote the marker; it stays and is processed again.
        await catalog.DeleteAsync(path, version, cancellationToken).ConfigureAwait(false);

        // A failed copy left by an earlier run is obsolete now.
        await catalog.DeleteAsync(markers.FailedPathOf(marker.Spec, marker.SubjectId, marker.SpanStart), null, cancellationToken)
            .ConfigureAwait(false);
        return Outcome.Processed;
    }

    private async Task<Outcome> FailAsync(Marker marker, int attempts, Exception ex, CancellationToken cancellationToken)
    {
        var path = marker.Entry.Path;
        var content = new MarkerContent(attempts, ex.Message);
        if (attempts < configuration.MaxAttempts)
        {
            try
            {
                await markers.WriteAsync(path, content, marker.Entry.Version, cancellationToken).ConfigureAwait(false);
            }
            catch (StorageVersionMismatchException)
            {
                // A raw write reset the marker meanwhile; its fresh attempt count wins.
            }

            Report(AggregationFailureKind.AttemptFailed, marker, attempts, ex);
            return Outcome.Retried;
        }

        var failedPath = markers.FailedPathOf(marker.Spec, marker.SubjectId, marker.SpanStart);
        await markers.WriteAsync(failedPath, content, null, cancellationToken).ConfigureAwait(false);
        if (!await catalog.DeleteAsync(path, marker.Entry.Version, cancellationToken).ConfigureAwait(false))
        {
            // A raw write reset the marker meanwhile, so the span gets a fresh set of attempts instead.
            await catalog.DeleteAsync(failedPath, null, cancellationToken).ConfigureAwait(false);
            Report(AggregationFailureKind.AttemptFailed, marker, attempts, ex);
            return Outcome.Retried;
        }

        Report(AggregationFailureKind.MovedToFailed, marker, attempts, ex);
        return Outcome.MovedToFailed;
    }

    private void Report(AggregationFailureKind kind, Marker marker, int attempt, Exception? ex)
        => configuration.Report(new AggregationFailure(kind, marker.Spec, marker.SubjectId, marker.SpanStart, null, attempt, ex));
}

/// <summary>Outcome of one <see cref="AggregationMaintenance.RecoverAsync"/> call.</summary>
/// <param name="RawFilesScanned">Raw files found in the range.</param>
/// <param name="MarkersSet">Stale aggregated files marked dirty.</param>
/// <param name="TempFilesRemoved">Orphaned temp files deleted from a <see cref="FileCatalog"/>.</param>
public readonly record struct RecoveryResult(int RawFilesScanned, int MarkersSet, int TempFilesRemoved);

/// <summary>A marker that failed its last allowed attempt.</summary>
/// <param name="Spec">The aggregation concerned.</param>
/// <param name="SubjectId">Subject of the aggregated file.</param>
/// <param name="StorageSpanStart">Start of the aggregated storage time span.</param>
/// <param name="Attempts">Failed attempts.</param>
/// <param name="LastError">Message of the last failure.</param>
/// <param name="FailedAt">When the marker moved to the failed folder.</param>
public sealed record FailedMarker(
    AggregationSpec Spec,
    string SubjectId,
    DateTime StorageSpanStart,
    int Attempts,
    string LastError,
    DateTimeOffset FailedAt);

/// <summary>Outcome of one <see cref="AggregationMaintenance.ProcessDirtyAsync"/> call, counted per marker.</summary>
/// <param name="Processed">Aggregated files recomputed.</param>
/// <param name="SkippedOpen">Markers left alone because their span is still open or within the grace period.</param>
/// <param name="Retried">Failed attempts that will be retried.</param>
/// <param name="MovedToFailed">Markers moved to the failed folder after the last allowed attempt.</param>
/// <param name="RejectedByRetention">Markers of spans whose raw files may be gone, removed without recomputing.</param>
/// <param name="Deferred">Failed markers left alone until their <see cref="AggregationConfiguration.RetryDelay"/> passes.</param>
public readonly record struct MarkerRunResult(
    int Processed,
    int SkippedOpen,
    int Retried,
    int MovedToFailed,
    int RejectedByRetention,
    int Deferred);
