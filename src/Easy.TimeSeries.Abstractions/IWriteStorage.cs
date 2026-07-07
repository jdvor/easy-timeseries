namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Destination for a serialized time-series buffer. Abstracts where the bytes go (local file, blob, memory) so the
/// writer stays storage-agnostic. The writer streams the header and each column via <see cref="WriteAsync"/>, then
/// calls <see cref="CloseAsync"/> once to finalize.
/// </summary>
public interface IWriteStorage
{
    /// <summary>Appends a chunk of the serialized output.</summary>
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>Signals that all data has been written and the destination may be flushed and closed.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
