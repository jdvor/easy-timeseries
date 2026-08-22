# Easy.TimeSeries.Parquet

Converts a serialized [Easy.TimeSeries](https://www.nuget.org/packages/Easy.TimeSeries) columnar buffer into an
Apache Parquet stream via [Parquet.Net](https://github.com/aloneguid/parquet-dotnet). Conversion is column-oriented
and reuses the core reader, so no schema or DTO needs to be supplied - the self-describing time-series header drives
the whole process.

Only the `ets -> parquet` direction is implemented today; `parquet -> ets` is not yet available.

## Quick start

```csharp
using Easy.TimeSeries.Parquet;
using Easy.TimeSeries.Storage;

using var source = new FileStorage("readings.ets");
await using var destination = File.Create("readings.parquet");

await TsToParquetConverter.ConvertAsync(source, destination);
```

`ParquetConversionOptions` controls `decimal` precision/scale and the Parquet row group size.

## Links

- [Type mapping and options](https://github.com/jdvor/easy-timeseries/blob/master/docs/parquet-conversion.md)
- [Source code](https://github.com/jdvor/easy-timeseries)
- [License](https://github.com/jdvor/easy-timeseries/blob/master/LICENSE)
