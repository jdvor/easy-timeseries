# AI Agent Instructions

This file is a primer for AI agents working on the `easy-timeseries` solution. It is intentionally short and
high-signal - it is not a substitute for the codebase or the docs. Claude Code reads it through `.claude/CLAUDE.md`,
OpenCode and Codex read it directly.

## Before you touch X, read Y

Only this file is loaded automatically. Read the matching document before working in an area:

| Area                                                  | Read                                                   |
| ----------------------------------------------------- | ------------------------------------------------------ |
| Any C# code                                           | `docs/code-style.md`                                   |
| `src/Easy.TimeSeries/Internal/`, `Writer`, `Reader`   | `docs/layout.md`, `.claude/rules/format-invariants.md` |
| `Storage/`, `Paths/`, AzureBlobs, storage contracts   | `docs/storage.md`, `.claude/rules/storage-invariants.md` |
| `src/Easy.TimeSeries.Aggregation/`                    | `docs/aggregation.md`; for `WriteTime/` also `.claude/rules/aggregation-invariants.md` |
| `src/Easy.TimeSeries.SrcGen/`                         | `docs/source-generation.md`                            |
| `src/Easy.TimeSeries.Parquet/`                        | `docs/parquet-conversion.md`                           |
| Benchmarks                                            | `docs/benchmarking.md`                                 |
| Public API, encodings, column options                 | `docs/introduction.md`                                 |
| Which project does what                               | `docs/solution-overview.md`                            |

## Rules and skills

Shared rules live in `.claude/rules/` and apply to every agent, whatever its harness:

| Rule                        | Applies to                                     |
| --------------------------- | ---------------------------------------------- |
| `git-commits.md`            | every commit and branch                        |
| `pull-requests.md`          | every PR description                           |
| `markdown.md`               | `*.md` files                                   |
| `mermaid.md`                | mermaid diagrams in `*.md`                     |
| `documenting-code.md`       | XML documentation in `*.cs`                    |
| `format-invariants.md`      | bit-level encoders/decoders and the file format |
| `storage-invariants.md`     | storage, paths, the Azure adapter              |
| `aggregation-invariants.md` | write-time aggregation                         |

Claude Code loads them natively - the last five only when a matching file is touched. OpenCode loads all of them
through `instructions` in `opencode.json`. Any other agent should read the relevant rule before the matching task.

Shared skills live in `.claude/skills/`: `csharp` (modern C# 14 / .NET 10 features) and `dotnet-test` (running tests
under Microsoft.Testing.Platform).

## Important principles

- Performance is a first-class concern. This is a specialized columnar storage library; speed, GC pressure, and
  memory usage matter more here than in typical line-of-business code. Prefer `Span<T>`, `ReadOnlySpan<T>`,
  `ArrayPool<T>.Shared`, `ref struct` readers, and avoid hidden allocations on hot paths.
- Keep low-level columnar APIs (per-type readers/writers, `Writer`, `Reader`) easy to use directly.
  Higher-level conveniences (DTO mapping, source generators, storage adapters, aggregation) sit on top in a
  "use-it-or-ignore-it" fashion - never make them mandatory.

## Facts that are easy to miss

- Every project has a working implementation. The one deliberate feature gap is the reverse Parquet direction
  (`parquet -> ets`).
- `Easy.TimeSeries.Abstractions` and `Easy.TimeSeries.SrcGen` target `netstandard2.0`; everything else `net10.0`.
- The CLI project `Easy.TimeSeries.CmdLine` builds an assembly named `ets`, so it grants `InternalsVisibleTo` to its
  test project explicitly - the shared `$(AssemblyName).Tests` rule does not match.
- `Easy.TimeSeries.AzureBlobs.Tests` runs against a real Azurite container via Testcontainers and needs Docker.

## Code map - what file names don't tell you

- `src/Easy.TimeSeries/Internal/` has one writer/reader pair per encoding. Writers are `sealed class`, readers are
  `ref struct` over a `ReadOnlySpan<byte>`. A new encoding means a new pair, not a branch in shared code.
- The `*Raw` column value types (`FloatRaw`, `DoubleRaw`, `Int32Raw`, `Int64Raw`) are what `NumberDistribution.Random`
  writes: raw values plus Brotli, for data uncorrelated row to row.
- `ScaledNumber32`/`ScaledNumber64` are the only lossy encoding; every other column round-trips exactly.
- `ReflectionBasedMaterializer` is a correctness baseline. Hot paths use the `{Dto}Materializer` emitted by
  `Easy.TimeSeries.SrcGen`.
- `PooledArrayBufferWriter` backs every column buffer and rents from `ArrayPool<byte>.Shared` - always dispose it.

## Public vs internal contract

- External consumers' DTOs reference only `Easy.TimeSeries.Abstractions`: the `[Column]`, `[GenerateWriter]` and
  `[GenerateReader]` attributes, `IMaterializer<T>`, `IWriter`, `ReadOptions`, the storage interfaces and the
  precision and distribution enums.
- Everything inside `Easy.TimeSeries.Internal` is `internal`. `src/Directory.Build.props` grants
  `InternalsVisibleTo` to the test assemblies, `Easy.TimeSeries.Benchmarks` and `Easy.TimeSeries.CmdLine`.
  Production code outside these assemblies must go through public APIs.

## Sibling repository: `easy-timeseries-js`

A read-only **JavaScript/TypeScript decoder** for the same `.ets` format lives in `jdvor/easy-timeseries-js`. It is
published to npm as `@jdvor/easy-timeseries` and ships an Apache ECharts adapter and a demo site. Writing `.ets` from
JavaScript is permanently out of scope there - this repository remains the only writer.

The work owed from **this** side: a `manifest.json` next to the golden fixtures, an always-on test that keeps it in
sync, golden coverage for the four `*Raw` column types, and a `docs/javascript-reader.md` page in the book.

**What crosses the boundary: the golden fixture corpus, and nothing else.** The JS repo vendors a copy of
`tests/data/golden/v1/` and refreshes it with its own `scripts/sync-fixtures.sh`.

## Build, test, run

- Target framework: `net10.0` (`shared.props`). SDK pinned via `global.json` to `10.0.300` with `latestFeature`
  roll-forward.
- Central package management: `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`, no
  `EnablePackageVersionOverride`). Do not put `Version=` on `<PackageReference>` in csproj files.
- Build: `dotnet build`. Analyzers run in the build; treat their warnings as work to fix, not suppress.
- Tests: `DOTNET_NOLOGO=1 dotnet test -c Release`, optionally with `--project <path to test csproj>`. This repo runs
  Microsoft.Testing.Platform, not VSTest - load the `dotnet-test` skill before running or debugging tests. Filter with
  xunit v3's `--filter-class` / `--filter-method` after a literal `--`, never with `--filter`.
- Golden format fixtures live in `tests/data/golden/v1`. Regenerate **only** for a deliberate format change:
  `./scripts/regen-golden.sh`, then review the diff.
- **A deliberate format change is a two-repo change.** Because `easy-timeseries-js` is a separate repository, a
  format change here will not fail its conformance suite in the same pull request - nothing enforces this
  automatically. After regenerating the fixtures, dispatch `sync-fixtures.yml` in that repo rather than leaving it
  to the weekly cron.
- Benchmarks: `dotnet run -c Release --project src/Easy.TimeSeries.Benchmarks`.
- CI, in `.github/workflows/`:
  - `test.yml` runs on PRs and pushes to `master`; pushes that touch only Markdown, scripts or editor config skip it.
  - `benchmarks.yml` is `workflow_dispatch` only and opens a `perf-results/` PR with the results.
  - `publish.yml` is `workflow_dispatch` only; the version comes from conventional commit prefixes (see
    `.claude/rules/git-commits.md`).

## Documentation

- `docs/` is an `mdbook` (`book.toml`, `SUMMARY.md`); `docs/readme.md` explains how to serve it locally.
- When adding non-trivial features or invariants, write them into `docs/` (and `SUMMARY.md`) or a path-scoped rule
  in `.claude/rules/`, rather than expanding this file.
- For external library docs (e.g. `Parquet.Net`, `ConsoleAppFramework`, `BenchmarkDotNet`, `xunit.v3`, Azure SDK),
  verify the current API against official documentation rather than relying on memory.
