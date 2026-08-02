namespace Easy.TimeSeries.AzureBlobs;

using Azure.Storage.Blobs;
using Paths;

public sealed class AzureBlobStorageFactory
{
    private readonly BlobContainerClient containerClient;
    private readonly PathBuilder pathBuilder;

    public TimeGranularity Granularity { get; }

    public AzureBlobStorageFactory(string connectionString, string containerName, TimeGranularity granularity)
        : this(new BlobContainerClient(connectionString, containerName), granularity)
    {
    }

    public AzureBlobStorageFactory(BlobContainerClient containerClient, TimeGranularity granularity)
    {
        Granularity = granularity;
        this.containerClient = containerClient;
        pathBuilder = new PathBuilder(granularity);
    }

    public AzureBlobStorage Create(DateTime fromUtcInclusive)
    {
        var path = pathBuilder.GetExpectedFilePath(fromUtcInclusive);
        var blobClient = containerClient.GetBlobClient(path);
        return new AzureBlobStorage(blobClient);
    }
}
