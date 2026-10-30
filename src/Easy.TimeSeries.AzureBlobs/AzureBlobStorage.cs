namespace Easy.TimeSeries.AzureBlobs;

using Abstractions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

/// <summary>
/// Azure Blob backing for both writing and reading a serialized buffer. Reads download the whole blob into memory.
/// </summary>
/// <remarks>
/// Writes are buffered in pooled memory and uploaded once on <see cref="CloseAsync"/>, so the blob changes
/// atomically: readers see either the old or the new content, and disposing without <see cref="CloseAsync"/> leaves
/// the blob untouched. <c>BlobClient.OpenWriteAsync</c> is not used because it replaces the blob with an empty one
/// as soon as it opens.
/// </remarks>
public sealed class AzureBlobStorage : IVersionedWriteStorage, IReadStorage
{
    private const int InitialBufferSize = 64 * 1024;

    private readonly BlobClient blobClient;
    private readonly ETag? ifMatch;
    private PooledArrayBufferWriter? buffer;
    private bool disposed;

    public AzureBlobStorage(BlobClient blobClient)
        : this(blobClient, ifMatch: null)
    {
    }

    /// <param name="ifMatch">When set, the upload on close commits only if the blob still has this ETag.</param>
    internal AzureBlobStorage(BlobClient blobClient, ETag? ifMatch)
    {
        this.blobClient = blobClient;
        this.ifMatch = ifMatch;
    }

    /// <summary>ETag of the uploaded blob; <c>null</c> before a successful close.</summary>
    public StorageVersion? CommittedVersion { get; private set; }

    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        buffer ??= new PooledArrayBufferWriter(Math.Max(InitialBufferSize, data.Length), 1f, Array.MaxLength);
        data.Span.CopyTo(buffer.GetSpan(data.Length));
        buffer.Advance(data.Length);
        return Task.CompletedTask;
    }

    /// <exception cref="StorageVersionMismatchException">The blob no longer has the expected ETag.</exception>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (buffer is null)
        {
            return;
        }

        var options = new BlobUploadOptions();
        if (ifMatch is { } etag)
        {
            options.Conditions = new BlobRequestConditions { IfMatch = etag };
        }

        try
        {
            var response = await blobClient.UploadAsync(new BinaryData(buffer.WrittenMemory), options, cancellationToken)
                .ConfigureAwait(false);
            CommittedVersion = AzureBlobCatalog.ToVersion(response.Value.ETag);
        }
        catch (RequestFailedException ex) when (ifMatch is not null && ex.Status is 404 or 412)
        {
            throw new StorageVersionMismatchException(blobClient.Name, ex);
        }
        finally
        {
            ReleaseBuffer();
        }
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken)
    {
        var response = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
        return response.Value.Content.ToMemory();
    }

    /// <summary>Releases the write buffer; without a preceding <see cref="CloseAsync"/> nothing is uploaded.</summary>
    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            ReleaseBuffer();
        }

        return ValueTask.CompletedTask;
    }

    public override string ToString() => blobClient.Uri.ToString();

    private void ReleaseBuffer()
    {
        buffer?.Dispose();
        buffer = null;
    }
}
