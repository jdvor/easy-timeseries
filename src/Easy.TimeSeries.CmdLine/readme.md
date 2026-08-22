# Easy.TimeSeries CmdLine (`ets`)

Developer command-line tool for [Easy.TimeSeries](https://www.nuget.org/packages/Easy.TimeSeries): generates
reference files and converts between formats, mainly for debugging the on-disk layout and testing downstream
tooling (e.g. Parquet consumers).

## Install

```shell
dotnet tool install --global Easy.TimeSeries.CmdLine
```

## Commands

- `ets ref-files-ts` - generates reference `.ets` files from built-in sample data.
- `ets ref-files-parquet` - generates reference Parquet files from the same sample data.
- `ets convert-to-parquet` - converts an existing `.ets` file to Apache Parquet.

Run `ets <command> --help` for the arguments each command accepts.

## Links

- [Getting started](https://github.com/jdvor/easy-timeseries/blob/master/docs/introduction.md)
- [Source code](https://github.com/jdvor/easy-timeseries)
- [License](https://github.com/jdvor/easy-timeseries/blob/master/LICENSE)
