namespace Easy.TimeSeries.AzureBlobs;

using Abstractions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Storage;
using System.Runtime.CompilerServices;

/// <summary>
/// <see cref="IStorageCatalog"/> over one blob container; logical paths are blob names. Versions are blob ETags,
/// and conditional writes and deletes use an ETag precondition, which the service checks atomically.
/// </summary>
public sealed class AzureBlobCatalog : IStorageCatalog
{
    private readonly BlobContainerClient containerClient;

    public AzureBlobCatalog(string connectionString, string containerName)
        : this(new BlobContainerClient(connectionString, containerName))
    {
    }

    public AzureBlobCatalog(BlobContainerClient containerClient)
    {
        ArgumentNullException.ThrowIfNull(containerClient);
        this.containerClient = containerClient;
    }

    public async IAsyncEnumerable<StorageEntry> ListAsync(
        string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StoragePath.Validate(prefix, allowPrefix: true, nameof(prefix));
        var options = new GetBlobsOptions { Prefix = prefix.Length == 0 ? null : prefix };
        await foreach (var item in containerClient.GetBlobsAsync(options, cancellationToken).ConfigureAwait(false))
        {
            var properties = item.Properties;
            yield return new StorageEntry(
                item.Name,
                properties.LastModified ?? DateTimeOffset.MinValue,
                properties.ContentLength ?? 0,
                ToVersion(properties.ETag ?? default));
        }
    }

    public async Task<StorageEntry?> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        StoragePath.Validate(path);
        try
        {
            var response = await containerClient.GetBlobClient(path)
                .GetPropertiesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var properties = response.Value;
            return new StorageEntry(path, properties.LastModified, properties.ContentLength, ToVersion(properties.ETag));
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public IReadStorage OpenRead(string path)
    {
        StoragePath.Validate(path);
        return new AzureBlobStorage(containerClient.GetBlobClient(path));
    }

    public IWriteStorage OpenWrite(string path, StorageVersion? ifMatch = null)
    {
        StoragePath.Validate(path);
        return new AzureBlobStorage(containerClient.GetBlobClient(path), ifMatch is { } v ? new ETag(v.Value) : null);
    }

    public async Task<bool> DeleteAsync(
        string path,
        StorageVersion? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        StoragePath.Validate(path);
        var conditions = ifMatch is { } v ? new BlobRequestConditions { IfMatch = new ETag(v.Value) } : null;
        try
        {
            var response = await containerClient.GetBlobClient(path)
                .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, conditions, cancellationToken)
                .ConfigureAwait(false);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // A stale ETag fails with 412, also when the blob no longer exists.
            return false;
        }
    }

    public override string ToString() => containerClient.Uri.ToString();

    // The header form is quoted; list results and response headers then compare equal.
    internal static StorageVersion ToVersion(ETag etag) => new(etag.ToString("H"));
}
