# AI Agent Instructions

This file is a primer for AI agents working on the `easy-timeseries` solution. It is intentionally short and
high-signal - it is not a substitute for the codebase or the docs. Claude Code reads it through `.claude/CLAUDE.md`,
OpenCode and Codex read it directly.

## Read these first

- `README.md` at the solution root - what this library is for, supported types, links to docs.
- `docs/introduction.md` - getting started, and how to choose the encoding per column.
- `docs/solution-overview.md` - per-project responsibilities.
- `docs/code-style.md` - C# style conventions enforced in this repo.
- `docs/layout.md` - byte-level on-disk format; read before touching `Internal/`.

## Rules and skills

Shared rules live in `.claude/rules/` and apply to every agent, whatever its harness:

| Rule                  | Applies to                   |
| --------------------- | ---------------------------- |
| `git-commits.md`      | every commit and branch      |
| `pull-requests.md`    | every PR description         |
| `markdown.md`         | `*.md` files                 |
| `mermaid.md`          | mermaid diagrams in `*.md`   |
| `documenting-code.md` | XML documentation in `*.cs`  |

Claude Code loads them natively, OpenCode through `instructions` in `opencode.json`. Any other agent should read the
relevant rule before the matching task.

Shared skills live in `.claude/skills/`: `csharp` (modern C# 14 / .NET 10 features) and `dotnet-test` (running tests
under Microsoft.Testing.Platform).

## Important principles

- Performance is a first-class concern. This is a specialized columnar storage library; speed, GC pressure, and
  memory usage matter more here than in typical line-of-business code. Prefer `Span<T>`, `ReadOnlySpan<T>`,
  `ArrayPool<T>.Shared`, `ref struct` readers, and avoid hidden allocations on hot paths.
- Keep low-level columnar APIs (per-type readers/writers, `Writer`, `Reader`) easy to use directly.
  Higher-level conveniences (DTO mapping, source generators, storage adapters, aggregation) sit on top in a
  "use-it-or-ignore-it" fashion - never make them mandatory.

## Projects

`docs/solution-overview.md` lists every project and what it is for. Facts that are easy to miss:

- Every project has a working implementation. The one deliberate feature gap is the reverse Parquet direction
  (`parquet -> ets`), tracked as planned work in `docs/parquet-conversion.md`.
- `Easy.TimeSeries.Abstractions` and `Easy.TimeSeries.SrcGen` target `netstandard2.0`; everything else `net10.0`.
- The CLI project `Easy.TimeSeries.CmdLine` builds an assembly named `ets`, so it grants `InternalsVisibleTo` to its
  test project explicitly - the shared `$(AssemblyName).Tests` rule does not match.
- `Easy.TimeSeries.AzureBlobs.Tests` runs against a real Azurite container via Testcontainers and needs Docker.
- `Easy.TimeSeries.Benchmarks` is both a `BenchmarkDotNet` runner and a `size` / `list` command that compares file
  sizes against CSV and snappy Parquet.

## Sibling repository: `easy-timeseries-js`

A read-only **JavaScript/TypeScript decoder** for the same `.ets` format lives in `jdvor/easy-timeseries-js`. It is
published to npm as `@jdvor/easy-timeseries` and ships an Apache ECharts adapter and a demo site. Writing `.ets` from
JavaScript is permanently out of scope there - this repository remains the only writer.

The work owed from **this** side: a `manifest.json` next to the golden fixtures, an always-on test that keeps it in
sync, golden coverage for the four `*Raw` column types, and a `docs/javascript-reader.md` page in the book.

**What crosses the boundary: the golden fixture corpus, and nothing else.** The JS repo vendors a copy of
`tests/data/golden/v1/` and refreshes it with its own `scripts/sync-fixtures.sh`.

## Core library layout (`src/Easy.TimeSeries/`)

Top-level - public surface:

- `Writer.cs` - builder that accumulates one columnar stream per call (`AddTimeOrdered`, `AddInt32`, `AddCategory`,
  ...) and flushes to `IWriteStorage`.
- `Reader.cs` - static `Reader.ReadFromAsync` walks the header, dispatches each column to its decoder and pushes values
  into an `IMaterializer<T>`.
- `Header.cs`, `ColumnInfo.cs`, `Column.cs`, `ColumnValueType.cs`, `Version.cs`, `TimePrecision.cs` - file-level
  metadata, versioning, column descriptors.
- `ReflectionBasedMaterializer.cs` - reference implementation of `IMaterializer<T>` and a correctness baseline. Hot
  paths use the `{Dto}Materializer` emitted by `Easy.TimeSeries.SrcGen` instead.
- `PooledArrayBufferWriter.cs` - `IBufferWriter<byte>` backed by `ArrayPool<byte>.Shared`, used by every column.
- `Storage/` - `FileStorage`, `InMemoryStorage`, `FileCatalog`, `InMemoryCatalog`, `StoragePath`, `PathLocks`.
  The contracts (`IWriteStorage`, `IVersionedWriteStorage`, `IReadStorage`, `IStorageCatalog`) live in Abstractions.
- `Paths/` - `PathBuilder` and `TimeGranularity` for the bucketed path layout.
- `Extensions.cs`, `Exceptions.cs`, `AssemblyInfo.cs`.

`Internal/` - per-type encoders/decoders and shared bit-level primitives. Marked `internal` and only exposed to
tests, benchmarks and the CLI via `InternalsVisibleTo`.

- `BitWriter.cs`, `BitReader.cs` - pack/unpack values into 64-bit words.
- `ColumnHeader.cs` - 9-byte per-column prefix (`DataLength`, `Records`, `BitsInLastWord`).
- One writer/reader pair per encoding:
  - `Int32*`, `Int64*`, `Float*`, `Double*` - XOR-delta.
  - `Xor64*` - shared XOR-delta core.
  - `ScaledNumber32*`, `ScaledNumber64*` - fixed-point, the only lossy encoding.
  - `DateTimeOrdered*` (delta-of-delta), `DateTimeUnordered*` (full width), `TimeSpan*`.
  - `Decimal*`, `Bool*`, `Category*`.
- The `*Raw` column value types (`FloatRaw`, `DoubleRaw`, `Int32Raw`, `Int64Raw`) are the `Random` distribution:
  raw values plus Brotli, for data uncorrelated row to row.
- `CategoryMap.cs`, `IdAccumulator.cs` - label to short-id dictionary for the `Category` column type.
- `Block.cs`, `Constants.cs`, `Util.cs`, `Expect.cs`, `TypeCache.cs` - shared helpers; `TypeCache` caches compiled
  property setters for `ReflectionBasedMaterializer`.

## Public vs internal contract

- External consumers' DTOs reference only `Easy.TimeSeries.Abstractions`: the `[Column]`, `[GenerateWriter]` and
  `[GenerateReader]` attributes, `IMaterializer<T>`, `IWriter`, `ReadOptions`, the storage interfaces and the
  precision and distribution enums.
- `Easy.TimeSeries` depends on `Easy.TimeSeries.Abstractions`; everything inside `Easy.TimeSeries.Internal` is
  `internal`.
- `src/Directory.Build.props` grants `InternalsVisibleTo` to `$(AssemblyName).Tests`, `$(AssemblyName).IntegrationTests`,
  `Easy.TimeSeries.Benchmarks` and `Easy.TimeSeries.CmdLine`. Production code outside these assemblies must go through
  public APIs.

## Format invariants worth knowing before changing bit-level code

Read these before touching anything in `Internal/`:

- A file is one `Header` followed by N column blocks. Each column block is one `ColumnHeader` (9 bytes) followed by
  `DataLength` bytes of packed words. Words are 8 bytes, little-endian.
- `BitWriter.CommitRecord()` must be called **once per logical entry** by each domain writer (not once per
  `BitWriter.Write` call). Domain writers may call `Write` several times per entry (e.g. `CategoryWriter` writes a
  prefix bit plus the id; `Int32Writer` writes a block descriptor plus the value). Forgetting `CommitRecord` makes
  `ColumnHeader.Records` wrong and breaks the reader.
- `ColumnHeader.TotalBits` is derived from `DataLength` (bytes) and `BitsInLastWord`; do not multiply `DataLength`
  by `WordBitSize` - it is already byte-counted.
- `Constants.WordByteSize = 8`, `Constants.WordBitSize = 64`. Timestamp encoding has a fixed `Epoch = 2000-01-01 UTC`
  and a 41-bit ceiling - see `Constants.TimeStamp`.
- `CreateWriters` in `Writer` must request `sizeHint + ColumnHeader.Size` because the first `ColumnHeader.Size`
  bytes of each column buffer are reserved for the header, written at `Flush`.
- For `ScaledNumber32`/`ScaledNumber64`, `ColumnInfo.Meta` stores `decimalPlaces` (e.g. `2`), not the scale
  (e.g. `100`). Both writer and reader sides must do `scale = (int)Math.Pow(10, decimalPlaces)`.

## Storage and path conventions

See `docs/storage.md` for the full picture. Worth knowing before touching `Storage/`, `Paths/` or the Azure adapter:

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

## Write-time aggregation invariants

See `docs/aggregation.md`. Worth knowing before changing `src/Easy.TimeSeries.Aggregation/WriteTime/`:

- `HookedWriteStorage.CloseAsync` must never throw because of aggregation once the inner raw commit succeeded.
  Everything after the commit falls back to setting a marker and reporting through `OnFailure`.
- The processor deletes a marker only with `ifMatch` = the version it listed. Dropping that condition loses raw data
  written during a recompute. Every marker write must change the version.
- Markers are the only path to recomputing a longer storage time span. Recovery sets markers; it never recomputes.
- Raw retention is checked against the span **start**: the oldest raw files of a span are deleted first.
  `AggregationConfiguration.Validate` requires retention of at least the longest storage time span plus the grace
  period.
- Recovery decides staleness from source records (`{subject}.sources.json` next to each aggregated `.ets`), the raw
  paths and versions an aggregated file was computed from, never from timestamps. Every write of an aggregated file
  by the hook or the processor must be followed by its record, with versions taken no later than the raw files were
  read.
- A source record also names the aggregated version it describes, taken from `CommittedVersion` of that write. The
  hook binds its kept bytes to the raw `CommittedVersion` the same way; a `GetAsync` after a commit proves nothing.
- Post-commit work in the hook runs with `CancellationToken.None`: the raw file is committed, so stopping halfway
  would leave aggregations neither computed nor marked.

## Build, test, run

- Target framework: `net10.0` (`shared.props`). SDK pinned via `global.json` to `10.0.300` with `latestFeature`
  roll-forward.
- Central package management: `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`, no
  `EnablePackageVersionOverride`). Do not put `Version=` on `<PackageReference>` in csproj files.
- Build: `dotnet build`.
- Tests: `DOTNET_NOLOGO=1 dotnet test -c Release` for everything, or add
  `--project tests/Easy.TimeSeries.Tests/Easy.TimeSeries.Tests.csproj` for one project.
- **This repo runs Microsoft.Testing.Platform**, not VSTest (`global.json` sets `test.runner`). VSTest-only options
  (`--nologo`, `--logger`, `--filter`, `--collect`, `--blame`, `--settings`) make the run exit before discovery with
  `Zero tests ran` and exit code 5 (*invalid arguments*, not an empty run). Filter with xunit v3's own
  `--filter-class` / `--filter-method` after a literal `--`. The `dotnet-test` skill has the details.
- Golden format fixtures live in `tests/data/golden/v1`. Regenerate **only** for a deliberate format change:
  `./scripts/regen-golden.sh`, then review the diff.
- **A deliberate format change is a two-repo change.** Because `easy-timeseries-js` is a separate repository, a
  format change here will not fail its conformance suite in the same pull request - nothing enforces this
  automatically. After regenerating the fixtures, dispatch `sync-fixtures.yml` in that repo rather than leaving it
  to the weekly cron.
- Benchmarks: `dotnet run -c Release --project src/Easy.TimeSeries.Benchmarks`. See `docs/benchmarking.md`.
- CI, in `.github/workflows/`:
  - `test.yml` runs on PRs and pushes to `master`; pushes that touch only Markdown, scripts or editor config skip it.
  - `benchmarks.yml` is `workflow_dispatch` only and opens a `perf-results/` PR with the results.
  - `publish.yml` is `workflow_dispatch` only; the version comes from conventional commit prefixes (see
    `.claude/rules/git-commits.md`).

## Code conventions enforced by tooling

`docs/code-style.md` is the full guide. The tooling-enforced part:

- File-scoped namespace declaration with `using` directives **inside** the namespace
  (`csharp_using_directive_placement = inside_namespace:warning` in `.editorconfig`). Example:
  `namespace Easy.TimeSeries;` then `using ...;` below it.
- `Nullable enable`, `ImplicitUsings enable`, `LangVersion=latest`.
- Banned APIs in `src/BannedSymbols.txt`: `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`,
  `DateTimeOffset.UtcNow`. Use `TimeProvider.GetUtcNow` / `GetLocalNow`.
- Analyzers: `Microsoft.CodeAnalysis.NetAnalyzers`, `Microsoft.CodeAnalysis.BannedApiAnalyzers`, xUnit analyzers;
  `AnalysisMode=Recommended`, `EnforceCodeStyleInBuild=true`. Treat analyzer warnings as work to fix, not suppress,
  unless an existing pattern in `.editorconfig` already suppresses the rule.
- Argument validation: prefer the lightweight helpers in `Internal/Expect.cs` (`Expect.Range`, `Expect.NotNull`,
  `Expect.Utc`, ...) over hand-rolled `throw` statements. They are `AggressiveInlining` + `DebuggerStepThrough`.

## Documentation

- `docs/` is an `mdbook` (`book.toml`, `SUMMARY.md`); `docs/readme.md` explains how to serve it locally.
- When adding non-trivial features or invariants, write them into `docs/` (and `SUMMARY.md`) rather than expanding
  this file.
- For external library docs (e.g. `Parquet.Net`, `ConsoleAppFramework`, `BenchmarkDotNet`, `xunit.v3`, Azure SDK),
  verify the current API against official documentation rather than relying on memory.
