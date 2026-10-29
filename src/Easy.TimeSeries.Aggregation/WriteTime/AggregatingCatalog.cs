namespace Easy.TimeSeries.Aggregation;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Aggregation.WriteTime;
using Easy.TimeSeries.Paths;

/// <summary>
/// Write-time aggregation hook: decorates a storage catalog so that every raw file written through it updates the
/// aggregations. Aggregated files with the raw storage time span are written right after the raw file; longer ones
/// get a dirty marker, processed later by <see cref="AggregationMaintenance"/>.
/// </summary>
/// <remarks>
/// <para>
/// Works with the plain <see cref="Writer"/> and every generated <c>IWriter&lt;T&gt;</c>, because both write through
/// <see cref="IWriteStorage"/>. <c>CloseAsync</c> of a raw file never fails because of aggregation: once it returns,
/// the raw file is committed, and aggregation problems go to <see cref="AggregationConfiguration.OnFailure"/>.
/// </para>
/// <para>
/// Every member other than <see cref="OpenWrite"/> of a raw path delegates to the inner catalog unchanged.
/// </para>
/// </remarks>
public sealed class AggregatingCatalog : IStorageCatalog
{
    private readonly IStorageCatalog inner;
    private readonly AggregationConfiguration configuration;
    private readonly MarkerStore markers;
    private readonly SourceStore sources;
    private readonly (AggregationSpec Spec, PathBuilder Paths, Downsampler? Immediate)[] specs;
    private readonly bool keepsBytes;

    /// <exception cref="ArgumentException">The configuration is inconsistent.</exception>
    public AggregatingCatalog(IStorageCatalog inner, AggregationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(configuration);
        this.inner = inner;
        this.configuration = configuration.Validate();
        markers = new MarkerStore(inner, configuration);
        sources = new SourceStore(inner, configuration);
        specs =
        [
            .. configuration.Specs.Select(spec => (
                spec,
                spec.CreatePathBuilder(),
                spec.StorageTimeSpan == configuration.Raw.Granularity ? new Downsampler(spec, configuration.Raw.Granularity) : null)),
        ];
        keepsBytes = specs.Any(x => x.Immediate is not null);
    }

    /// <summary>Opens the raw file of a bucket and subject for writing, through the hook.</summary>
    public IWriteStorage OpenRawWrite(DateTime bucketStart, string subjectId)
        => OpenWrite(configuration.Raw.GetFilePath(bucketStart, subjectId));

    /// <summary>
    /// Opens <paramref name="path"/> for writing. A path that parses as a raw file gets the hooked storage; any other
    /// path is passed through unchanged.
    /// </summary>
    public IWriteStorage OpenWrite(string path, StorageVersion? ifMatch = null)
    {
        var storage = inner.OpenWrite(path, ifMatch);
        return configuration.Raw.TryParse(path, out var bucketStart, out var subjectId)
            ? new HookedWriteStorage(this, storage, path, bucketStart, subjectId, keepsBytes)
            : storage;
    }

    public IAsyncEnumerable<StorageEntry> ListAsync(string prefix, CancellationToken cancellationToken = default)
        => inner.ListAsync(prefix, cancellationToken);

    public Task<StorageEntry?> GetAsync(string path, CancellationToken cancellationToken = default)
        => inner.GetAsync(path, cancellationToken);

    public IReadStorage OpenRead(string path) => inner.OpenRead(path);

    /// <summary>Deletes like the inner catalog; deleting a raw file, e.g. for retention, marks nothing dirty.</summary>
    public Task<bool> DeleteAsync(string path, StorageVersion? ifMatch = null, CancellationToken cancellationToken = default)
        => inner.DeleteAsync(path, ifMatch, cancellationToken);

    public override string ToString() => $"aggregating {inner}";

    /// <summary>
    /// Runs after the raw file is committed. Never throws: failures set a marker where possible and are reported.
    /// </summary>
    /// <param name="rawVersion">
    /// Version the raw commit produced, which binds <paramref name="bytes"/> to it; <c>null</c> when the inner catalog
    /// does not report it, and then equal storage time spans get a marker instead of an immediate aggregation.
    /// </param>
    /// <remarks>
    /// Runs without the caller's cancellation token: the raw file is already committed, and stopping halfway would
    /// leave aggregations neither computed nor marked.
    /// </remarks>
    internal async Task AfterRawCommitAsync(
        string rawPath,
        DateTime bucketStart,
        string subjectId,
        ReadOnlyMemory<byte> bytes,
        StorageVersion? rawVersion)
    {
        foreach (var (spec, paths, immediate) in specs)
        {
            var spanStart = paths.GetBucketStart(bucketStart);
            if (immediate is null || rawVersion is null)
            {
                await SetMarkerAsync(spec, subjectId, spanStart, rawPath, AggregationFailureKind.MarkerWriteFailed, null).ConfigureAwait(false);
                continue;
            }

            try
            {
                if (!await AggregateImmediatelyAsync(spec, immediate, paths, rawPath, bucketStart, subjectId, bytes, rawVersion.Value)
                        .ConfigureAwait(false))
                {
                    await SetMarkerAsync(spec, subjectId, spanStart, rawPath, AggregationFailureKind.MarkerWriteFailed, null).ConfigureAwait(false);
                }
            }
#pragma warning disable CA1031 // the raw file is committed; aggregation failures are reported, never thrown
            catch (Exception ex)
#pragma warning restore CA1031
            {
                await SetMarkerAsync(spec, subjectId, spanStart, rawPath, AggregationFailureKind.ImmediateFailed, ex).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Aggregates the kept bytes of an equal storage time span. Returns <c>false</c> when another writer committed
    /// the same raw path since <paramref name="rawVersion"/>, so the result may be stale and the span must be
    /// recomputed from storage. Otherwise records the raw and aggregated versions, so recovery sees the file as current.
    /// </summary>
    private async Task<bool> AggregateImmediatelyAsync(
        AggregationSpec spec,
        Downsampler downsampler,
        PathBuilder paths,
        string rawPath,
        DateTime bucketStart,
        string subjectId,
        ReadOnlyMemory<byte> bytes,
        StorageVersion rawVersion)
    {
        StorageVersion? aggregatedVersion;
        await using (var target = inner.OpenWrite(paths.GetFilePath(bucketStart, subjectId)))
        {
            await downsampler.RunAsync(bucketStart, new RawBuffer(bucketStart, bytes, rawPath), target).ConfigureAwait(false);
            aggregatedVersion = (target as IVersionedWriteStorage)?.CommittedVersion;
        }

        // The bytes belong to rawVersion; a later raw commit means the aggregated file may hold outdated data.
        var current = await inner.GetAsync(rawPath).ConfigureAwait(false);
        if (current?.Version != rawVersion)
        {
            return false;
        }

        await sources.WriteAsync(spec, subjectId, bucketStart, [(rawPath, rawVersion)], aggregatedVersion, CancellationToken.None)
            .ConfigureAwait(false);
        return true;
    }

    private async Task SetMarkerAsync(
        AggregationSpec spec,
        string subjectId,
        DateTime spanStart,
        string rawPath,
        AggregationFailureKind kindOnFailure,
        Exception? cause)
    {
        try
        {
            await markers.SetAsync(spec, subjectId, spanStart, CancellationToken.None).ConfigureAwait(false);
            if (cause is not null)
            {
                configuration.Report(new AggregationFailure(kindOnFailure, spec, subjectId, spanStart, rawPath, 0, cause));
            }
        }
#pragma warning disable CA1031 // recovery finds the stale file; the raw write must not fail
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (cause is not null)
            {
                configuration.Report(new AggregationFailure(kindOnFailure, spec, subjectId, spanStart, rawPath, 0, cause));
            }

            configuration.Report(new AggregationFailure(
                AggregationFailureKind.MarkerWriteFailed, spec, subjectId, spanStart, rawPath, 0, ex));
        }
    }
}
