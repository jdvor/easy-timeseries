namespace Easy.TimeSeries.Benchmarks.Size;

/// <summary>
/// Byte sizes of one dataset serialized to the compared formats.
/// Parquet is written with the library default compression (Snappy); the Easy.TimeSeries format is uncompressed.
/// </summary>
public sealed record SizeResult(string Dataset, int Rows, long CsvBytes, long ParquetBytes, long EtsBytes);
