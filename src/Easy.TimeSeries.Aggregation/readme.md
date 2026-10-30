# Easy.TimeSeries.Aggregation

Aggregated time series for [Easy.TimeSeries](https://www.nuget.org/packages/Easy.TimeSeries): collapses raw readings
into one row per aggregation time span (e.g. one per minute), per group, using predefined functions (average, min,
max, median, quantiles, standard deviation, ...) or custom multi-column functions. An aggregated file is an ordinary
`.ets` file.

## Quick start

```csharp
using Easy.TimeSeries.Aggregation;
using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;

var spec = new AggregationSpec(
        aggregationTimeSpan: AggregationTimeSpan.Minute,
        storageTimeSpan: TimeGranularity.Day)
    .GroupBy("Sensor")
    .Column("Voltage", Agg.Avg, Agg.Min, Agg.Max, Agg.Q95);

var catalog = new FileCatalog(new DirectoryInfo("/data"));
var raw = new PathBuilder("raw", TimeGranularity.Hour);
var downsampler = new Downsampler(spec, rawStorageTimeSpan: TimeGranularity.Hour);

// reads raw/2026/10/25/{00..23}/sensor-a.ets, writes agg-1m-daily/2026/10/25/sensor-a.ets
await downsampler.RunAsync(catalog, raw, "sensor-a", new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc));
```

## Links

- [Aggregation](https://github.com/jdvor/easy-timeseries/blob/master/docs/aggregation.md)
- [Source code](https://github.com/jdvor/easy-timeseries)
- [License](https://github.com/jdvor/easy-timeseries/blob/master/LICENSE)
