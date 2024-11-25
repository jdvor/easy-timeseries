namespace Easy.TimeSeries.Storage;

public interface IWriteStorage
{
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    Task CloseAsync(CancellationToken cancellationToken = default);
}
