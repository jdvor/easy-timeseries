---
paths:
  - "src/Easy.TimeSeries/Storage/**"
  - "src/Easy.TimeSeries/Paths/**"
  - "src/Easy.TimeSeries.AzureBlobs/**"
  - "src/Easy.TimeSeries.Abstractions/**"
  - "tests/**/StorageCatalogContract.cs"
---

# Storage and Path Invariants

Worth knowing before touching storage, paths, the Azure adapter or the storage contracts. The design is in
`docs/storage.md`.

- Storage paths are logical: always `/`-separated and relative, on every OS. `PathBuilder` never uses
  `Path.Combine`; only `FileCatalog` maps logical paths to OS paths.
- Subject ids are lowercase `[a-z0-9_-]+` and `PathBuilder.TryParse` is case-sensitive. `GetFilePath` must stay the
  exact inverse of `TryParse`, including for an empty root.
- Every write is atomic, and disposing an `IWriteStorage` without `CloseAsync` aborts it. `FileStorage` writes a
  `.{name}.{guid}.tmp` file and renames it on close. `AzureBlobStorage` buffers and uploads once on close.
- Do not use `BlobClient.OpenWriteAsync`: it uploads an empty blob when it opens, which truncates the target and
  consumes any `IfMatch` condition before the data is written.
- `FileStorage` commits and `FileCatalog` deletes run under `PathLocks`, so a version check and the rename or delete
  are atomic within the process. Every commit and delete of a local file must take that lock, conditional or not.
- Storages returned by `OpenWrite` implement `IVersionedWriteStorage`. Take the version of your own write from
  `CommittedVersion`, never from a `GetAsync` after the commit.
- `IStorageCatalog` implementations share one contract test, `tests/Easy.TimeSeries.Tests/StorageCatalogContract.cs`,
  which the AzureBlobs test project links in. A new catalog derives from it.
- Abstractions targets `netstandard2.0`: no default interface methods. `IAsyncDisposable` and `IAsyncEnumerable` come
  from `Microsoft.Bcl.AsyncInterfaces`.
