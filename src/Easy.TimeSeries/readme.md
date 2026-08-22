# Easy.TimeSeries

Columnar storage for numeric time-series data, tuned for size and speed. Aimed at large volumes of sensor-style data
that need to be visualized or analyzed later on.

Each column is compressed with the encoding that fits its shape:

- Integer compression - delta, delta-of-delta, Simple-8b, run-length encoding.
- Floating point compression - XOR-based (Gorilla-style) delta encoding.
- Data-agnostic compression - dictionary compression for low-cardinality categorical values.

Supported column types: `Boolean`, `Int32`, `Int64`, `Double`, `Float`, `DateTime`, `TimeSpan`, and `String`
(dictionary-encoded).

## Quick start

```csharp
using Easy.TimeSeries;
using Easy.TimeSeries.Storage;

using var writer = new Writer(rows: samples.Count)
    .AddTimeOrdered(samples.Select(s => s.TimestampUtc), "ts")
    .AddScaledNumber32(samples.Select(s => s.Celsius), "temp", decimalPlaces: 2)
    .AddCategory(samples.Select(s => s.SensorId), "sensor");

using var storage = new FileStorage("readings.ets");
await writer.WriteToAsync(storage);

var readings = await Reader.ReadFromAsync(storage, new ReflectionBasedMaterializer<Reading>());
```

For source-generated, allocation-conscious writers/readers on top of a DTO, see
[Easy.TimeSeries.SrcGen](https://www.nuget.org/packages/Easy.TimeSeries.SrcGen).

## Links

- [Getting started](https://github.com/jdvor/easy-timeseries/blob/master/docs/introduction.md)
- [Data layout details](https://github.com/jdvor/easy-timeseries/blob/master/docs/layout.md)
- [Source code](https://github.com/jdvor/easy-timeseries)
- [License](https://github.com/jdvor/easy-timeseries/blob/master/LICENSE)
