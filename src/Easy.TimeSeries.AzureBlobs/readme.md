# Easy.TimeSeries.AzureBlobs

Azure Blob Storage adapter for [Easy.TimeSeries](https://www.nuget.org/packages/Easy.TimeSeries): implements
`IWriteStorage`/`IReadStorage` over a single blob, so `Writer`/`Reader` can persist directly to Azure Blob Storage
instead of the local file system.

- `AzureBlobStorage` - wraps a `BlobClient` and implements `IWriteStorage`, `IReadStorage`, `IAsyncDisposable`.
- `AzureBlobStorageFactory` - creates an `AzureBlobStorage` for a UTC timestamp, laying blobs out by
  `TimeGranularity` (mirrors the local `PathBuilder` layout used by file storage).

## Quick start

```csharp
using Easy.TimeSeries.AzureBlobs;
using Easy.TimeSeries.Paths;

var factory = new AzureBlobStorageFactory(connectionString, containerName, TimeGranularity.Daily);

await using var storage = factory.Create(fromUtcInclusive: timeProvider.GetUtcNow().UtcDateTime);
await writer.WriteToAsync(storage);
```

## Links

- [Getting started](https://github.com/jdvor/easy-timeseries/blob/master/docs/introduction.md)
- [Source code](https://github.com/jdvor/easy-timeseries)
- [License](https://github.com/jdvor/easy-timeseries/blob/master/LICENSE)
