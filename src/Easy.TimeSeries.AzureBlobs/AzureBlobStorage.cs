namespace Easy.TimeSeries.AzureBlobs;

using Abstractions;

public sealed class AzureBlobStorage : IWriteStorage, IReadStorage
{
    private readonly string blobPath;

    public AzureBlobStorage(string blobPath)
    {
        this.blobPath = blobPath;
    }

    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override string ToString() => blobPath;
}
