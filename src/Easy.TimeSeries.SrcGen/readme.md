# Easy.TimeSeries.SrcGen

Roslyn incremental source generator that bridges row-based DTO classes and the
[Easy.TimeSeries](https://www.nuget.org/packages/Easy.TimeSeries) columnar storage APIs. Annotate a DTO once and the
generator emits allocation-conscious, strongly typed implementations of the
[Easy.TimeSeries.Abstractions](https://www.nuget.org/packages/Easy.TimeSeries.Abstractions) contracts:

- `[GenerateWriter]` emits `{Dto}Writer : IWriter<{Dto}>`.
- `[GenerateReader]` emits `{Dto}Materializer : IMaterializer<{Dto}>` and `{Dto}Reader : IReader<{Dto}>`.

Generated classes are `public sealed partial`, live in the DTO's namespace, and can be extended with convenience
overloads in a separate file.

## Usage

```csharp
using Easy.TimeSeries.Abstractions;

[GenerateWriter]
[GenerateReader]
public sealed class Reading
{
    [Column(0, Label = "ts", DateTimeSort = DateTimeSort.Ascending)]
    public DateTime TimestampUtc { get; set; }

    [Column(1, Label = "temp", NumberPrecision = NumberPrecision.DecimalPlaces2)]
    public float Celsius { get; set; }

    [Column(2, Label = "sensor")]
    public string SensorId { get; set; } = string.Empty;
}
```

```csharp
using var storage = new FileStorage("readings.ets");
await new ReadingWriter().WriteAsync(samples, storage);
var readings = await new ReadingReader().ReadAsync(new ReadOptions(), storage);
```

This package only contributes an analyzer at build time (`DevelopmentDependency=true`); it does not add a runtime
dependency, but does require a reference to `Easy.TimeSeries` for the emitted code to compile.

## Links

- [Source generation reference](https://github.com/jdvor/easy-timeseries/blob/master/docs/source-generation.md)
- [Getting started](https://github.com/jdvor/easy-timeseries/blob/master/docs/introduction.md)
- [Source code](https://github.com/jdvor/easy-timeseries)
- [License](https://github.com/jdvor/easy-timeseries/blob/master/LICENSE)
