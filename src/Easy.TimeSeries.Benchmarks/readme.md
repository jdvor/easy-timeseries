# Easy.TimeSeries.Benchmarks

Performance and size harness for [Easy.TimeSeries](https://www.nuget.org/packages/Easy.TimeSeries). Not a shipped
package - it exists to answer two questions: how small is the format, and how fast are the encoders.

Both run over the shared datasets in `Easy.TimeSeries.TestData`, so the numbers come from real series rather than
synthetic input.

## Size comparison

Writes each dataset as CSV, as snappy-compressed Parquet, and as `.ets`, then reports the ratios.

```shell
dotnet run -c Release --project src/Easy.TimeSeries.Benchmarks -- size -o size-report.md
```

Omit `-o` to print the table to the console.

## Micro-benchmarks

One `BenchmarkDotNet` class per column encoder, each measuring a write and a read. `MemoryDiagnoser` is enabled,
so allocations and GC counts appear alongside timings - GC pressure matters as much as throughput here.

```shell
# everything, quick mode
dotnet run -c Release --project src/Easy.TimeSeries.Benchmarks

# one encoder, full BenchmarkDotNet defaults
dotnet run -c Release --project src/Easy.TimeSeries.Benchmarks -- --filter '*Int32*' --full
```

| Mode              | Job                                                | Use when                                  |
| ----------------- | -------------------------------------------------- | ----------------------------------------- |
| `--quick` default | 1 launch, 3 warmup, 5 measured iterations          | Iterating on a change                     |
| `--full`          | BenchmarkDotNet defaults (adaptive warmup, 15 runs) | Pinning a number worth quoting            |

Results land in a dated sub-directory of `artifacts/`.

## Both at once

`./scripts/run-benchmarks.sh` runs the size comparison and the micro-benchmarks and writes both reports to
`performance-results/<date>-<short-sha>/`, with a header recording the commit, branch, SDK and OS. The
`Benchmarks` GitHub Actions workflow runs the same script and opens a pull request with the results.

Shared CI runners are noisy - treat workflow timings as indicative and re-run locally before drawing conclusions.

## Links

- [Benchmarking](https://github.com/jdvor/easy-timeseries/blob/master/docs/benchmarking.md)
- [Source code](https://github.com/jdvor/easy-timeseries)
- [License](https://github.com/jdvor/easy-timeseries/blob/master/LICENSE)
