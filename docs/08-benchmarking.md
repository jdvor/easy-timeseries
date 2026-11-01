# Benchmarking

`src/Easy.TimeSeries.Benchmarks` doubles as two distinct measurements:

- **Size comparison** - deterministic, single-pass serialized size of the well-known test datasets across
  CSV, Parquet (snappy) and Easy.TimeSeries. See `SizeCommands`/`Size/DatasetSizer.cs`.
- **Micro-benchmarks** - BenchmarkDotNet timings for the bit-level encoders/decoders (`Int32Benchmark`,
  `CategoryBenchmark`, `BlockBenchmark`, ...).

Results are stored as markdown reports directly in the repository under `performance-results/`, so a
regression can be spotted by comparing reports across runs. GitHub Actions artifacts were considered
instead, but their ~90 day retention makes them unsuitable for long-term trend comparison.

## Running locally

```shell
./scripts/run-benchmarks.sh
```

| Flag                  | Meaning                                                                     |
| --------------------- | ---------------------------------------------------------------------------|
| `-o, --output-dir`    | Base output directory. Defaults to `performance-results` at the repo root. |
| `-f, --filter`        | BenchmarkDotNet filter pattern for the micro-benchmark run. Defaults to `*` (everything). |
| `-s, --size-only`     | Run only the size comparison.                                              |
| `-b, --bench-only`    | Run only the micro-benchmarks.                                             |
| `-F, --full`          | Run micro-benchmarks with BenchmarkDotNet's default job instead of the trimmed quick one. |

With neither `-s`/`-b` given, both run. The script writes into `performance-results/`, leaves the results
uncommitted, and does not touch git or open a PR - reviewing and committing them is up to you, typically
alongside the code change that motivated the run.

## Quick vs full precision

Micro-benchmarks run in **quick** mode by default: one launch, 3 warmup and 5 measured iterations
(`Config`, in `src/Easy.TimeSeries.Benchmarks/Config.cs`). Even so a full sweep takes several minutes, which
is why it is the default for day-to-day runs.

`-F`/`--full` switches to BenchmarkDotNet's default job - adaptive warmup and 15 measured iterations - which
takes considerably longer but produces tighter confidence intervals. Use it when a number is going to be
quoted or committed as a baseline, and prefer narrowing the run with `--filter` at the same time.

The flag only affects the micro-benchmarks; the size comparison is a deterministic single pass and is
unaffected. Passing it to the benchmark app directly works too:

```shell
dotnet run -c Release --project src/Easy.TimeSeries.Benchmarks -- --filter '*Int32*' --full
```

## Output layout

Each run produces one dated, commit-tagged folder:

```
performance-results/
  2026-08-22-ff98f32/
    size-report.md
    microbenchmarks.md
```

The folder name is `<UTC date>-<7-char short SHA>`. `size-report.md` starts with a short header (commit,
branch, .NET SDK version, OS) so it is self-describing even out of context; `microbenchmarks.md` keeps
BenchmarkDotNet's own header (runtime, host, job info) as-is. Re-running for the same date and commit
overwrites that folder.

**Retention:** there is no automated cleanup. Delete folders older than roughly a year by hand when they
stop being useful.

## Running in CI

The `Benchmarks` GitHub Actions workflow (`.github/workflows/benchmarks.yml`) is `workflow_dispatch`
only - it is not triggered by every push, because shared runners are noisy and unreliable for micro-benchmark
timing comparisons. Its inputs map onto the script flags:

| Input       | Values                            | Maps to                    |
| ----------- | --------------------------------- | -------------------------- |
| `mode`      | `all`, `size-only`, `bench-only`  | `-s` / `-b`                |
| `filter`    | any BenchmarkDotNet pattern       | `-f`                       |
| `precision` | `quick` (default), `full`         | `--full` when `full`       |

The workflow does not commit results directly to the trunk. It pushes them to a
disposable `perf-results/<date>-<sha>` branch and opens a pull request, so the numbers get a human review
before they land - again because of runner noise.
