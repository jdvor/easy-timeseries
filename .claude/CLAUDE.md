# AI Agent Instructions

This file is a primer for AI agents working on the `easy-timeseries` solution. It is intentionally short and
high-signal - it is not a substitute for the codebase or the docs.

## Read these first

- `README.md` at the solution root - what this library is for, supported types, links to docs.
- `docs/solution-overview.md` - per-project responsibilities.
- `docs/code-style.md` - C# style conventions enforced in this repo.
- `docs/layout.md` - byte-level on-disk format; read before touching `Internal/`.

## Important principles

- Performance is a first-class concern. This is a specialized columnar storage library; speed, GC pressure, and
  memory usage matter more here than in typical line-of-business code. Prefer `Span<T>`, `ReadOnlySpan<T>`,
  `ArrayPool<T>.Shared`, `ref struct` readers, and avoid hidden allocations on hot paths.
- Keep low-level columnar APIs (per-type readers/writers, `Writer`, `ReadBuilder`) easy to use directly.
  Higher-level conveniences (DTO mapping, source generators, storage adapters) sit on top in a
  "use-it-or-ignore-it" fashion - never make them mandatory.

## Project map (implementation status)

| Project                            | Status      | Notes                                                                                                                      |
| ---------------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------------- |
| `src/Easy.TimeSeries.Abstractions` | implemented | Stable contracts for external consumers: `ColumnAttribute`, `IMaterializer<T>`, `DataValueType`, precision enums.          |
| `src/Easy.TimeSeries`              | implemented | Core library. Bit-level encoders/decoders, `Header`, `Writer`, `ReadBuilder`, `ReflectionBasedMaterializer`, storage.      |
| `src/Easy.TimeSeries.SrcGen`       | implemented | Roslyn incremental generator: `[GenerateWriter]`/`[GenerateReader]` on a DTO emit `{Dto}Writer`, `{Dto}Materializer`, `{Dto}Reader`. Diagnostics ETS001-ETS011. See `docs/source-generation.md`. |
| `src/Easy.TimeSeries.AzureBlobs`   | implemented | `AzureBlobStorage` (IWriteStorage + IReadStorage) and `AzureBlobStorageFactory`, which maps a time bucket to a blob path via `PathBuilder`. |
| `src/Easy.TimeSeries.Parquet`      | implemented (ts->parquet) | `TsToParquetConverter` converts a ts buffer to Apache Parquet via `Parquet.Net`, reusing the core reader through an `IMaterializer` sink. Native type mapping (TIMESTAMP/DECIMAL/etc.). Reverse direction (parquet->ts) not yet done. |
| `src/Easy.TimeSeries.CmdLine`      | implemented | `ets` dotnet tool on `ConsoleAppFramework`: `ref-files-ets`, `ref-files-parquet`, `convert-to-parquet`.                     |
| `src/Easy.TimeSeries.Benchmarks`   | implemented | `BenchmarkDotNet` micro-benchmarks per column encoder, plus an `ets size` command comparing against CSV and snappy Parquet. |
| `src/Easy.TimeSeries.TestData`     | implemented | Re-usable sample datasets (Boeing, Gold, Vix, Macro4, PowerPlant) shared by tests and benchmarks.                          |
| `src/Easy.Sample`                  | implemented | Small example app demonstrating source-generated writer/reader end to end (`PowerNode` DTO).                               |
| `tests/Easy.TimeSeries.Tests`      | implemented | xUnit v3 tests for the core library: bit-level writers/readers, DTO round-trip, storage, and golden format fixtures.       |
| `tests/Easy.TimeSeries.SrcGen.Tests` | implemented | `CSharpGeneratorDriver`-based tests for the generator: generated-code shape, ETS diagnostics, incrementality.            |
| `tests/Easy.TimeSeries.Parquet.Tests` | implemented | Conversion tests for `TsToParquetConverter`, including native type mapping.                                             |
| `tests/Easy.TimeSeries.AzureBlobs.Tests` | implemented | Integration tests against a real Azurite container via Testcontainers; needs Docker to run.                          |

Every project in the solution has a working implementation. The one deliberate feature gap is the reverse Parquet
direction (`parquet -> ets`), tracked as planned work in `docs/parquet-conversion.md`.

## Core library layout (`src/Easy.TimeSeries/`)

Top-level - public surface:

- `Writer.cs` - builder that accumulates one columnar stream per call (`AddTime`, `AddInt32`, `AddCategory`, ...) and
  flushes to `IWriteStorage`.
- `ReadBuilder.cs` - dispatches per-column readers and pushes values into an `IMaterializer<T>` to materialize DTOs.
- `Header.cs`, `ColumnInfo.cs`, `Column.cs`, `ColumnValueType.cs`, `Version.cs`, `TimePrecision.cs` - file-level
  metadata, versioning, column descriptors.
- `ReflectionBasedMaterializer.cs` - reference implementation of `IMaterializer<T>`; the source-generated variant will
  replace it for hot paths.
- `PooledArrayBufferWriter.cs` - `IBufferWriter<byte>` backed by `ArrayPool<byte>.Shared`, used by every column.
- `Storage/` - `IWriteStorage`, `IReadStorage`, `FileStorage`, `InMemoryStorage`.
- `Paths/` - `PathBuilder` and `TimeGranularity` for layout of files on disk.
- `Extensions.cs`, `Exceptions.cs`, `AssemblyInfo.cs`.

`Internal/` - per-type encoders/decoders and shared bit-level primitives. Marked `internal` and only exposed to
tests/benchmarks/CmdLine via `InternalsVisibleTo`.

- `BitWriter.cs`, `BitReader.cs` - pack/unpack values into 64-bit words.
- `ColumnHeader.cs` - 9-byte per-column prefix (`DataLength`, `Records`, `BitsInLastWord`).
- One pair per supported type: `Int32Writer`/`Int32Reader`, `Int64*`, `Float*`, `Double*`, `ScaledNumber32*`,
  `ScaledNumber64*`, `DateTime*`, `TimeSpan*`, `Bool*`, `Category*`.
- `CategoryMap.cs`, `IdAccumulator.cs` - label↔short-id dictionary for the `Category` column type.
- `Block.cs`, `Constants.cs`, `Util.cs`, `Expect.cs`, `TypeCache.cs` - shared helpers; `TypeCache` caches compiled
  property setters for `ReflectionBasedMaterializer`.

## Public vs internal contract

- External consumers' DTOs reference only `Easy.TimeSeries.Abstractions` (attributes + `IMaterializer<T>` + enums).
- `Easy.TimeSeries` depends on `Easy.TimeSeries.Abstractions`; everything inside `Easy.TimeSeries.Internal/` is `internal`.
- `src/Directory.Build.props` grants `InternalsVisibleTo` to `*.Tests`, `*.IntegrationTests`, `Easy.TimeSeries.Benchmarks`,
  `Easy.TimeSeries.CmdLine`. Production code outside these four assemblies must go through public APIs.

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

## Build, test, run

- Target framework: `net10.0` (`shared.props`). SDK pinned via `global.json` to `10.0.300` with `latestFeature` roll-forward.
- Central package management: `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`, no
  `EnablePackageVersionOverride`). Do not put `Version=` on `<PackageReference>` in csproj files.
- Build: `dotnet build`.
- Tests: `DOTNET_NOLOGO=1 dotnet test -c Release` for everything, or add
  `--project tests/Easy.TimeSeries.Tests/Easy.TimeSeries.Tests.csproj` for one project.
- **This repo runs Microsoft.Testing.Platform**, not VSTest (`global.json` sets `test.runner`). VSTest-only options
  (`--nologo`, `--logger`, `--filter`, `--collect`, `--blame`, `--settings`) are forwarded to the test app, which
  rejects them and exits **before discovery**. The symptom is misleading - `Zero tests ran` with exit code 5, which
  means *invalid arguments*, not an empty run (that would be exit code 8). Suppress the banner with the
  `DOTNET_NOLOGO=1` environment variable; filter with xunit v3's own `--filter-class` / `--filter-method` after a
  literal `--`.
- Golden format fixtures live in `tests/data/golden/v1`. Regenerate **only** for a deliberate format change:
  `./scripts/regen-golden.sh`, then review the diff.
- Benchmarks: `dotnet run -c Release --project src/Easy.TimeSeries.Benchmarks`.
- CI: `.github/workflows/test.yml` runs on push/PR (paths-ignore for docs/scripts). `.github/workflows/publish.yml` is
  `workflow_dispatch` only; semantic version is derived from conventional commit prefixes (`feat:`, `BREAKING CHANGE:`)
  via `paulhatch/semantic-version`.

## Code conventions enforced by tooling

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

## Documentation state

- `docs/` is set up as an `mdbook` (`book.toml`, `SUMMARY.md`, `scripts/install-mdbook.sh`).
- `solution-overview.md`, `license.md`, `code-style.md`, and `layout.md` have content. `introduction.md` is still
  a `TBD` stub. The `README.md` at the solution root is the most up-to-date narrative overview.
- When adding non-trivial features or invariants, prefer writing them into `docs/` (and updating `SUMMARY.md`)
  over expanding this file.

## AI tooling preferences

- Use the user's global rules in `~/.claude/rules/` (`roslyn.md`, `context7.md`, `verify-api.md`, `common-tools.md`,
  `git-commits.md`, `conversation-format.md`, `complex-tasks.md`) - they apply to this repo and override generic
  defaults.
- For C# navigation, prefer `cwm-roslyn-navigator` MCP tools (`find_symbol`, `find_references`, `get_diagnostics`,
  `get_project_graph`) over reading files or shelling out to `dotnet build`.
- For external library docs (e.g. `Parquet.Net`, `ConsoleAppFramework`, `BenchmarkDotNet`, `xunit.v3`), use `context7` MCP
  first; for Microsoft/Azure APIs use `microsoft-docs` MCP.
- Commits follow the format in `~/.claude/rules/git-commits.md` (`type: header [JIRA-ID?]`, body explains why).
  Conventional prefixes also drive semantic versioning in CI.

## Writing or editing markdown files
- Keep line width at 130 characters maximum.
- Except for markdown tables, leave those as they are and do not try them to format to same width (130).
