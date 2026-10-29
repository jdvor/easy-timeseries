# Easy.TimeSeries CmdLine (`ets`)

Developer command-line tool for [Easy.TimeSeries](https://www.nuget.org/packages/Easy.TimeSeries): generates
reference files and converts between formats, mainly for debugging the on-disk layout and testing downstream
tooling (e.g. Parquet consumers).

## Install

```shell
dotnet tool install --global Easy.TimeSeries.Tool
```

## Commands

- `ets ref-files-ets` - generates reference `.ets` files from built-in sample data.
- `ets ref-files-parquet` - generates reference Parquet files from the same sample data.
- `ets convert-to-parquet` - converts an existing `.ets` file to Apache Parquet.
- `ets downsample` - aggregates a directory of raw files into one aggregated file per storage time span and subject.

Run `ets <command> --help` for the arguments each command accepts.

## Downsample

```shell
ets downsample --input ./data/raw --output ./data --raw-granularity hour \
    --aggregation 1m --storage day --from 2026-10-01 --to 2026-11-01 \
    --group Sensor --columns Voltage=avg+min+max+q95,Current=avg+stddev,Status=last,Ok
```

- `--input` holds raw files in the `PathBuilder` layout, e.g. `2026/10/25/14/sensor-a.ets` for hourly files.
- Output goes to `--output/agg-{aggregation}-{storage}/...`, e.g. `agg-1m-daily/2026/10/25/sensor-a.ets`.
- `--columns` separates columns with commas and functions with `+`. Functions: `avg`, `median`, `min`, `max`,
  `stddev`, `trueratio`, `last`, `q95`, `q99`, `q<percent>` (e.g. `q99.9`). A column without functions gets the
  default of its type.
- Every subject found in the range is aggregated, or only `--subject`.
- Existing aggregated files are skipped unless `--force` is passed.
- A failing file is reported and the run continues; the exit code is 1 if any file failed.

Custom multi-column functions need code; see the
[aggregation guide](https://github.com/jdvor/easy-timeseries/blob/master/docs/aggregation.md).

## Links

- [Getting started](https://github.com/jdvor/easy-timeseries/blob/master/docs/introduction.md)
- [Source code](https://github.com/jdvor/easy-timeseries)
- [License](https://github.com/jdvor/easy-timeseries/blob/master/LICENSE)
