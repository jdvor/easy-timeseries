namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// A strongly-typed writer for a specific DTO. The source generator emits an implementation per
/// <see cref="GenerateWriterAttribute"/>-annotated type that encodes a collection of rows and flushes it to storage.
/// </summary>
/// <typeparam name="T">The row type being serialized.</typeparam>
public interface IWriter<T>
    where T : class
{
    /// <summary>Encodes <paramref name="data"/> as columns and writes the result to <paramref name="storage"/>.</summary>
    Task WriteAsync(ICollection<T> data, IWriteStorage storage, CancellationToken cancellationToken = default);
}

/// <summary>
/// A strongly-typed reader for a specific DTO. The source generator emits an implementation per
/// <see cref="GenerateReaderAttribute"/>-annotated type that decodes a buffer from storage into row instances.
/// </summary>
/// <typeparam name="T">The row type being materialized.</typeparam>
public interface IReader<T>
    where T : class
{
    /// <summary>Reads the buffer from <paramref name="storage"/> and materializes the rows, honoring <paramref name="options"/>.</summary>
    Task<T[]> ReadAsync(ReadOptions options, IReadStorage storage, CancellationToken cancellationToken = default);
}
