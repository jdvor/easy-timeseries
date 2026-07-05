namespace Easy.TimeSeries.Abstractions;

public interface IWriter<T>
    where T : class
{
    Task WriteAsync(ICollection<T> data, IWriteStorage storage, CancellationToken cancellationToken = default);
}

public interface IReader<T>
    where T : class
{
    Task<T[]> ReadAsync(ReadOptions options, IReadStorage storage, CancellationToken cancellationToken = default);
}
