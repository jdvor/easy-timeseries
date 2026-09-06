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

[Here][tscae] is an easy read on those techniques.

Supported data types in columnar storage:

- `Boolean`
- `Int32`
- `Int64`
- `Double`
- `Float`
- `DateTime`
- `TimeSpan`
- `String` (low cardinality value mapped to an integer through a dictionary)

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