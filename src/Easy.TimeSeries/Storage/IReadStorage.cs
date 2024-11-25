namespace Easy.TimeSeries.Storage;

public interface IReadStorage
{
    Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken);
}
