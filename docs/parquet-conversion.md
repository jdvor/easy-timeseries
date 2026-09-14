# Parquet Conversion

`Easy.TimeSeries.Parquet` converts a serialized time-series buffer (this library's columnar format) into an Apache
Parquet stream. It is meant for the common workflow of keeping data in the compact time-series format most of the
time and materializing a Parquet copy on demand, when the data needs to be plugged into an analytical framework
(Spark, DuckDB, Arrow, pandas, ...).

Conversion is file-to-file and driven entirely by the self-describing time-series header - no schema or DTO has to
be supplied. Only the `ts -> parquet` direction exists today; `parquet -> ts` is future work.

## Usage

`TsToParquetConverter` exposes two `ConvertAsync` overloads: one reads from any `IReadStorage`, the other from an
in-memory buffer. Both write to a caller-provided, writable and seekable `Stream`.

```csharp
using Easy.TimeSeries.Parquet;
using Easy.TimeSeries.Storage;

// From storage (local file, blob, in-memory, ...) to a Parquet file.
var source = new FileStorage("readings.ets");
await using var output = File.Create("readings.parquet");
await TsToParquetConverter.ConvertAsync(source, output);

// Or straight from an in-memory buffer.
ReadOnlyMemory<byte> buffer = ...;
await TsToParquetConverter.ConvertAsync(buffer, output);
```

Conversion reuses the core `Reader`: an internal `IMaterializer` sink accumulates each decoded column into a
strongly-typed array, which is then written as one Parquet column. Because the whole existing decode path is
reused, every column type the reader understands is supported without duplicating any decoder.

## Type mapping

Each time-series column maps to one Parquet column using a native Parquet type. Column names come from the column
label; an unlabeled column is named `col{index}` (e.g. `col0`).

| Column value type                      | Parquet field       | Physical / logical type              | Notes                                            |
| -------------------------------------- | ------------------- | ------------------------------------ | ------------------------------------------------ |
| `DateTimeOrdered`, `DateTimeUnordered` | `DateTimeDataField` | TIMESTAMP (INT64, UTC)               | Native timestamp; ordering is not represented    |
| `TimeSpan`                             | `DataField<long>`   | INT64                                | Stored as 100 ns ticks                           |
| `Float`, `FloatRaw`                    | `DataField<float>`  | FLOAT                                | Raw (Brotli) variant decodes identically         |
| `Double`, `DoubleRaw`                  | `DataField<double>` | DOUBLE                               | Raw (Brotli) variant decodes identically         |
| `Decimal`                              | `DecimalDataField`  | DECIMAL                              | Lossless; precision/scale from options           |
| `Int32`, `Int32Raw`                    | `DataField<int>`    | INT32                                |                                                  |
| `Int64`, `Int64Raw`                    | `DataField<long>`   | INT64                                |                                                  |
| `Bool`                                 | `DataField<bool>`   | BOOLEAN                              |                                                  |
| `ScaledNumber32`                       | `DataField<float>`  | FLOAT                                | Already lossy fixed-point; kept as native float  |
| `ScaledNumber64`                       | `DataField<double>` | DOUBLE                               | Already lossy fixed-point; kept as native double |
| `Category`                             | `DataField<string>` | BYTE_ARRAY (UTF-8), dictionary-coded |                                                  |

All fields are non-nullable - the time-series format stores a value for every row.

Notes on the deliberate choices:

- Date-times become the native Parquet TIMESTAMP logical type rather than a plain `INT64` of epoch milliseconds.
  Physically it *is* an INT64 epoch value, but the logical annotation makes analytical engines read the column as a
  timestamp instead of an anonymous integer. Since `TimePrecision` tops out at milliseconds, the millisecond
  TIMESTAMP unit covers every case.
- `ScaledNumber32`/`ScaledNumber64` map to `float`/`double`. They are already a lossy fixed-point representation, so
  a native floating-point column is the natural, smallest fit. `Decimal` (a lossless 128-bit value) instead maps to
  the native DECIMAL type to stay lossless.

## Options

`ParquetConversionOptions` is optional; every property has a sane default, so `ConvertAsync` works without it.

| Option             | Default | Effect                                                                                                               |
| ------------------ | ------- | -------------------------------------------------------------------------------------------------------------------- |
| `DecimalPrecision` | `38`    | Total digits for `Decimal` columns mapped to the Parquet DECIMAL type.                                               |
| `DecimalScale`     | `18`    | Fractional digits for `Decimal` columns mapped to the Parquet DECIMAL type.                                          |
| `RowGroupSize`     | `null`  | When set to a positive value, rows are split into row groups of at most that size. `null` writes a single row group. |

```csharp
var options = new ParquetConversionOptions
{
    DecimalPrecision = 18,
    DecimalScale = 4,
    RowGroupSize = 100_000,
};
await TsToParquetConverter.ConvertAsync(source, output, options);
```

## Planned: `parquet -> ets`

Only the `ets -> parquet` direction is implemented. The reverse is intentionally deferred rather than half-built,
because it needs three design decisions that the forward direction never has to make - the time-series header is
self-describing, a Parquet schema is not.

**Null handling.** Parquet columns are nullable by default and the ets format has no null slot. The options are to
reject a column containing nulls, or to substitute a documented per-type default and record that the column was
lossy. Silently writing zeros would be the worst of both.

**Encoding selection.** This is the hard one. Parquet carries no equivalent of the hints that make ets compress
well:

| ets needs to know       | Parquet tells us | Consequence                                                       |
| ----------------------- | ---------------- | ----------------------------------------------------------------- |
| Timestamps ascending?   | no               | Cannot choose between `AddTimeOrdered` and `AddTimeUnordered`     |
| Decimal places          | scale, sometimes | Cannot pick `ScaledNumber` precision reliably                     |
| Values correlated?      | no               | Cannot choose between the default and the `Random` distribution   |

Either the converter scans the data first and infers these (costly, and a wrong guess is silently expensive), or
the caller supplies a column mapping, which makes the API considerably less convenient than the forward direction.

**Logical type coverage.** Parquet has substantially more logical types than ets has column types. The mapping
table above must gain a documented inverse, including which types are rejected outright.

Until those are settled, treat Parquet as an export target: ingestion goes through `Writer`, where the encoding
hints are explicit.
