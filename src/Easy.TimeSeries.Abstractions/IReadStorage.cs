namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Source of a serialized time-series buffer. Abstracts where the bytes come from (local file, blob, memory) so the
/// reader stays storage-agnostic. Implementations return the whole buffer in one call.
/// </summary>
public interface IReadStorage
{
    /// <summary>Reads the complete serialized buffer.</summary>
    Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken);
}
