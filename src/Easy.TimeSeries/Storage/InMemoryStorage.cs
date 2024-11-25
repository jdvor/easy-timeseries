namespace Easy.TimeSeries.Storage;

using static Easy.TimeSeries.Constants;

internal sealed class InMemoryStorage : IWriteStorage, IReadStorage, IDisposable
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
            throw new InvalidOperationException();
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
            throw new InvalidOperationException();
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
