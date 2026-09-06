# Easy TimeSeries

Small framework / ecosystem for storing numeric time series data in a columnar fashion with several compression algorithms
to achieve maximal performance and minimal size (both on disk and on the wire).

The most typical use case for the framework is handling large volume of sensor data, which need to be visualized or analyzed later on.

The following techniques are used in the library:

- Integer compression:
  - Delta encoding
  - Delta-of-delta encoding
  - Simple-8b
  - Run-length encoding

- Floating point compression:
  - XOR-based compression

- Data-agnostic compression:
  - Dictionary compression
  - Brotli over the raw values, for columns whose values are uncorrelated row to row

[Here][tscae] is an easy read on those techniques.

Supported data types in columnar storage:

| Type       | Notes                                                                            |
| ---------- | -------------------------------------------------------------------------------- |
| `Boolean`  | Bit-packed, one bit per value                                                    |
| `Int32`    | Delta / XOR encoded; a `Random` variant stores uncorrelated values raw + Brotli  |
| `Int64`    | As `Int32`                                                                       |
| `Float`    | XOR-delta ("Gorilla"); a `Random` variant for uncorrelated values                |
| `Double`   | As `Float`                                                                       |
| `Decimal`  | Stored exactly, no precision loss                                                |
| `DateTime` | Delta-of-delta when ascending, full-width otherwise; selectable precision        |
| `TimeSpan` | Selectable precision                                                             |
| `String`   | Low-cardinality values mapped to an integer through a dictionary                 |

Real numbers can also be stored as a fixed-point *scaled number* (`ScaledNumber32` / `ScaledNumber64`), which
quantizes to a set number of decimal places. It is usually the smallest option, and the only lossy one - every
other encoding round-trips exactly. See [Getting started](docs/introduction.md) for how to choose per column.

## Users

- [Getting started](docs/introduction.md)
- [How the library is licensed](docs/license.md)
- [How to report security issues](SECURITY.md)

## Contributors

- [I want to contribute](CONTRIBUTING.md)
- [Solution Overview](docs/solution-overview.md)
- [Data Layout Details](docs/layout.md)
- [Code Style](docs/code-style.md)
- [Benchmarking](docs/benchmarking.md)

[tscae]: https://www.tigerdata.com/blog/time-series-compression-algorithms-explained