# Easy.TimeSeries.Abstractions

Public contracts for the [Easy.TimeSeries](https://www.nuget.org/packages/Easy.TimeSeries) ecosystem: attributes and
interfaces used to describe DTOs and materialize rows, independent of the storage implementation.

## Contents

- `ColumnAttribute` - marks a DTO property as a column and configures its precision/sort options.
- `IMaterializer<T>` - contract for turning a decoded row into a `T` instance.
- `DataValueType` and precision enums (`NumberPrecision`, `DateTimePrecision`, `TimeSpanPrecision`, `DateTimeSort`,
  `NumberDistribution`) - the tuning knobs read by both the core library and the source generator.

This package has no dependency on `Easy.TimeSeries` itself, so DTO projects that only need the attributes can
reference it without pulling in the storage/encoding implementation.

## Links

- [Getting started](https://github.com/jdvor/easy-timeseries/blob/master/docs/introduction.md)
- [Source code](https://github.com/jdvor/easy-timeseries)
- [License](https://github.com/jdvor/easy-timeseries/blob/master/LICENSE)
