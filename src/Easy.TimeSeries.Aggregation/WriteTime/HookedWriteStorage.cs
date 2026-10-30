namespace Easy.TimeSeries.Aggregation.WriteTime;

using Easy.TimeSeries.Abstractions;

/// <summary>
/// Write storage of a raw file opened through <see cref="AggregatingCatalog"/>: writes through to the inner storage,
/// keeps a pooled copy of the bytes when an aggregation needs them, and runs the hook after the inner commit.
/// </summary>
internal sealed class HookedWriteStorage(
    AggregatingCatalog owner,
    IWriteStorage inner,
    string path,
    DateTime bucketStart,
    string subjectId,
    bool keepBytes) : IVersionedWriteStorage
{
    public StorageVersion? CommittedVersion => (inner as IVersionedWriteStorage)?.CommittedVersion;

    private PooledArrayBufferWriter? buffer;
    private bool disposed;

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await inner.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        if (keepBytes)
        {
            buffer ??= new PooledArrayBufferWriter(Math.Max(64 * 1024, data.Length), 1f, Array.MaxLength);
            data.Span.CopyTo(buffer.GetSpan(data.Length));
            buffer.Advance(data.Length);
        }
    }

    /// <summary>Commits the raw file, then aggregates; only a failure of the raw commit itself is thrown.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await inner.CloseAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await owner.AfterRawCommitAsync(
                    path, bucketStart, subjectId, buffer?.WrittenMemory ?? ReadOnlyMemory<byte>.Empty, CommittedVersion)
                .ConfigureAwait(false);
        }
        finally
        {
            buffer?.Dispose();
            buffer = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        buffer?.Dispose();
        buffer = null;
        await inner.DisposeAsync().ConfigureAwait(false);
    }

    public override string ToString() => path;
}
