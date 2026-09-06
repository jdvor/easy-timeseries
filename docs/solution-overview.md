# Solution Overview

| path                             | type                   | contains                                                               |
| -------------------------------- | ---------------------- | ---------------------------------------------------------------------- |
| src/Easy.TimeSeries.Abstractions | C# NuGet package       | Contracts for TimeSeries, so extending it is easy or at least possible |
| src/Easy.TimeSeries              | C# NuGet package       | Core library with most of the implementations                          |
| src/Easy.TimeSeries.SrcGen       | C# NuGet package       | Library source-generating reader and/or writer based on a DTO class    |
| src/Easy.TimeSeries.AzureBlobs   | C# NuGet package       | Library providing storage to Azure Blobs                               |
| src/Easy.TimeSeries.Parquet      | C# NuGet package       | Library with conversion support for TimeSeries -> Apache Parquet       |
| src/Easy.TimeSeries.TestData     | C# library             | Re-usable data collections for tests and benchmarks                    |
| src/Easy.TimeSeries.CmdLine      | C# console             | Developer's swiss knife tool around TimeSeries                         |
| src/Easy.TimeSeries.Benchmarks   | C# console             | Application that runs performance benchmarks                           |
| src/Easy.Sample                  | C# console             | Example application to demonstrate TimeSeries usage                    |
| tests/Easy.TimeSeries.Tests      | C# xUnit tests         | Core library: encoders, storage, golden format fixtures                |
| tests/Easy.TimeSeries.SrcGen.Tests | C# xUnit tests       | Generator output, ETS diagnostics, incrementality                      |
| tests/Easy.TimeSeries.Parquet.Tests | C# xUnit tests      | Parquet conversion and type mapping                                    |
| tests/Easy.TimeSeries.AzureBlobs.Tests | C# xUnit tests   | Azure Blob storage against Azurite via Testcontainers (needs Docker)   |
| docs/                            | mdbook, markdown files | Documentation for both TimeSeries users and contributors               |
| scripts/                         | Bash scripts           | Scripts used in GitHub Actions and/or locally from interactive shell   |
| .github/                         | GitHub yaml files      | CI instructions for GitHub Actions                                     |

## Easy.TimeSeries.Abstractions

The only package an external DTO needs to reference. Holds the contracts that must stay stable for consumers:
`ColumnAttribute` and the `[GenerateWriter]` / `[GenerateReader]` markers, `IMaterializer<T>`, the storage
interfaces `IWriteStorage` and `IReadStorage`, `ReadOptions`, and the precision and distribution enums.

Targets `netstandard2.0` so it can be referenced from the source generator as well as from modern consumers.

## Easy.TimeSeries

The core library and the only project that understands the on-disk format.

- `Writer` accumulates one columnar stream per `Add*` call and flushes them to an `IWriteStorage`.
- `Reader` walks the header, dispatches each column to its decoder, and pushes values into an `IMaterializer<T>`.
- `Header`, `ColumnInfo`, `ColumnValueType` and `Version` describe the file-level metadata.
- `Storage/` provides `FileStorage` and `InMemoryStorage`; `Paths/` maps timestamps onto a bucketed file layout.
- `Internal/` holds the bit-level primitives and one encoder/decoder pair per supported type. It is `internal`,
  exposed only to the test, benchmark and CLI assemblies.

See [Data Layout Details](layout.md) for the byte format itself.

## Easy.TimeSeries.SrcGen

A Roslyn incremental generator. Annotating a DTO with `[GenerateWriter]` and/or `[GenerateReader]` emits a typed
`{Dto}Writer`, `{Dto}Materializer` and `{Dto}Reader`, so the reflection-based materializer can be avoided on hot
paths. Mistakes in the annotations are reported as `ETS001`-`ETS011` diagnostics rather than as broken generated
code. See [Source Generation](source-generation.md).

## Easy.TimeSeries.AzureBlobs

Mirrors the local-file storage in Azure Blob Storage. `AzureBlobStorage` implements both `IWriteStorage` and
`IReadStorage` over a `BlobClient`; `AzureBlobStorageFactory` turns a time bucket into a blob path using the same
`PathBuilder` the file layout uses, so a dataset laid out on disk and in a container has the same shape.

## Easy.TimeSeries.Parquet

Converts a serialized buffer into an Apache Parquet stream through `Parquet.Net`. The conversion is driven by the
self-describing header, so no DTO or schema has to be supplied, and each column maps onto a native Parquet type.
Only the `ets -> parquet` direction exists today. See [Parquet Conversion](parquet-conversion.md).

## Easy.TimeSeries.CmdLine

The `ets` dotnet tool - a developer's swiss knife around the format. Generates reference `.ets` and Parquet files
from the bundled sample data, and converts an existing `.ets` file to Parquet. Useful for inspecting the layout
and for feeding downstream tooling something real to read.

## Easy.TimeSeries.TestData

Sample datasets shared by the tests and the benchmarks - Boeing trades, gold and VIX prices, a macroeconomic
series, and a power-plant registry. Keeping them in one project stops the two consumers from drifting apart and
gives the size comparison realistic, non-synthetic input.

## Easy.TimeSeries.Benchmarks

Size comparison and micro-benchmarks, runnable locally or via the `Benchmarks` GitHub Actions workflow.
See [Benchmarking](benchmarking.md).

## Easy.Sample

A small end-to-end example: a `PowerNode` DTO annotated for source generation, written to storage and read back.
The shortest path to seeing the library work.
