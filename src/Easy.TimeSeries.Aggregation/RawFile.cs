namespace Easy.TimeSeries.Aggregation;

using Easy.TimeSeries.Abstractions;

/// <summary>A raw file to aggregate: its raw bucket start and where to read it from.</summary>
/// <param name="BucketStart">Start of the raw storage time span the file covers; every row must lie inside it.</param>
/// <param name="Storage">Source of the file's bytes.</param>
/// <param name="Name">Shown in error messages, e.g. the path.</param>
public readonly record struct RawFile(DateTime BucketStart, IReadStorage Storage, string? Name = null);

/// <summary>A raw file already in memory.</summary>
/// <param name="BucketStart">Start of the raw storage time span the buffer covers; every row must lie inside it.</param>
/// <param name="Buffer">Serialized raw file.</param>
/// <param name="Name">Shown in error messages.</param>
public readonly record struct RawBuffer(DateTime BucketStart, ReadOnlyMemory<byte> Buffer, string? Name = null);

/// <summary>Outcome of computing one aggregated file.</summary>
/// <param name="Rows">Aggregated rows written; 0 means no file was written.</param>
/// <param name="Groups">Distinct group values; 0 without grouping.</param>
/// <param name="RawFiles">Raw files read.</param>
/// <param name="RawRows">Raw rows aggregated.</param>
public readonly record struct AggregationResult(int Rows, int Groups, int RawFiles, long RawRows)
{
    /// <summary>Whether an aggregated file was written.</summary>
    public bool Written => Rows > 0;
}

/// <summary>A raw file found in a catalog listing, with the version it had when listed.</summary>
internal readonly record struct ListedRawFile(DateTime BucketStart, string Path, StorageVersion Version);
