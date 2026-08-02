namespace Easy.TimeSeries.AzureBlobs;

using Abstractions;
using Azure.Storage.Blobs;

/// <summary>
/// Azure Blob backing for both writing and reading a serialized buffer. Writes stream to a single blob as columns are
/// flushed; reads download the whole blob into memory. Dispose to release the underlying write stream.
/// </summary>
public sealed class AzureBlobStorage : IWriteStorage, IReadStorage, IAsyncDisposable
{
    private readonly BlobClient blobClient;
    private Stream? writeStream;
    private bool disposed;

    public AzureBlobStorage(BlobClient blobClient)
    {
        this.blobClient = blobClient;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        writeStream ??= await blobClient.OpenWriteAsync(overwrite: true, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await writeStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (writeStream is null)
        {
            return;
        }

        await writeStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        await writeStream.DisposeAsync().ConfigureAwait(false);
        writeStream = null;
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken)
    {
        var response = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
        return response.Value.Content.ToMemory();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (writeStream is not null)
        {
            await writeStream.DisposeAsync().ConfigureAwait(false);
            writeStream = null;
        }
    }

    public override string ToString() => blobClient.Uri.ToString();
}
