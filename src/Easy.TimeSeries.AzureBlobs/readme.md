# Easy.TimeSeries.AzureBlobs

Azure Blob Storage adapter for [Easy.TimeSeries](https://www.nuget.org/packages/Easy.TimeSeries). `Writer` and
`Reader` can persist directly to Azure Blob Storage instead of the local file system.

- `AzureBlobStorage` - wraps a `BlobClient`; implements `IWriteStorage` and `IReadStorage`. Writes are buffered and
  uploaded once on close, so a blob is replaced atomically and disposing without close leaves it untouched.
- `AzureBlobCatalog` - implements `IStorageCatalog` over a container: listing by prefix, ETag versions, and
  conditional writes and deletes checked by the service.

## Quick start

```csharp
using Easy.TimeSeries.AzureBlobs;
using Easy.TimeSeries.Paths;

var catalog = new AzureBlobCatalog(connectionString, containerName);
var paths = new PathBuilder("raw", TimeGranularity.Day);

var bucketStart = paths.GetBucketStart(timeProvider.GetUtcNow().UtcDateTime);
await using var storage = catalog.OpenWrite(paths.GetFilePath(bucketStart, "sensor-a"));
await writer.WriteToAsync(storage);
```

## Migrating from AzureBlobStorageFactory

| Before | After |
| --- | --- |
| `new AzureBlobStorageFactory(container, TimeGranularity.Hour)` | `new AzureBlobCatalog(container)` and `new PathBuilder(TimeGranularity.Hour)` |
| `factory.Create(bucketStart)` | `catalog.OpenWrite(paths.GetFilePath(bucketStart, subjectId))` |

The factory wrote `{date folders}/yyyyMMddHH.ets` without a subject id. Such blobs stay readable through
`catalog.OpenRead(path)` with their existing path.

## Links

- [Getting started](https://github.com/jdvor/easy-timeseries/blob/master/docs/introduction.md)
- [Storage](https://github.com/jdvor/easy-timeseries/blob/master/docs/storage.md)
- [Source code](https://github.com/jdvor/easy-timeseries)
- [License](https://github.com/jdvor/easy-timeseries/blob/master/LICENSE)
