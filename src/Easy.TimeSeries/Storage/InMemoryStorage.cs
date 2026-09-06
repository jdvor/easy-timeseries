namespace Easy.TimeSeries.Storage;

using Easy.TimeSeries.Abstractions;
using static Easy.TimeSeries.Constants;

/// <summary>
/// In-memory backing for both writing and reading, buffered by a pooled array. Handy for tests, round-trips, and
/// staging bytes before handing them to another sink. Write then read the same instance; dispose to return the buffer.
/// </summary>
public sealed class InMemoryStorage : IWriteStorage, IReadStorage, IDisposable
{
    private readonly PooledArrayBufferWriter buffer =
        new(DefaultInitialBufferSize, DefaultBufferGrowFactor, MaxAllowedBufferSize);

    private bool closed;
    private bool disposed;

    public int Size => buffer.WrittenCount;

    public ReadOnlyMemory<byte> WrittenMemory => buffer.WrittenMemory;

    public ReadOnlySpan<byte> WrittenSpan => buffer.WrittenSpan;

    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (closed)
        {
            throw new InvalidOperationException(
                "Cannot write to storage that has already been closed.");
        }

        var span = buffer.GetMemory(data.Length);
        data.CopyTo(span);
        buffer.Advance(data.Length);
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        closed = true;
        return Task.CompletedTask;
    }

    public Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (buffer.WrittenCount == 0)
        {
            throw new InvalidOperationException(
                "Cannot read from storage before anything has been written to it.");
        }

        return Task.FromResult(buffer.WrittenMemory);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        buffer.Dispose();
    }
}
