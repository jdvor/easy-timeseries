namespace Easy.TimeSeries.Abstractions;

public interface IReadStorage
{
    Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken);
}
