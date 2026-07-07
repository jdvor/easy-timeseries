namespace Easy.TimeSeries;

using System.Buffers;

/// <summary>
/// An <see cref="IBufferWriter{T}"/> backed by <see cref="ArrayPool{T}"/> to keep column serialization
/// allocation-free on the hot path. Grows by a configurable factor up to a hard ceiling, and returns its rented
/// array on <see cref="Dispose"/>. Not thread-safe; one instance backs one column.
/// </summary>
public sealed class PooledArrayBufferWriter : IBufferWriter<byte>, IDisposable
{
    private readonly float growFactor;
    private readonly int maxAllowedSize;
    private byte[] buffer;
    private bool disposed;

    /// <summary>Number of bytes committed via <see cref="Advance"/>.</summary>
    public int WrittenCount { get; private set; }

    internal int ResizedCount { get; private set; }

    /// <summary>Current capacity of the rented backing array.</summary>
    public int Capacity => buffer.Length;

    /// <summary>The written bytes as memory.</summary>
    public ReadOnlyMemory<byte> WrittenMemory => buffer.AsMemory(0, WrittenCount);

    /// <summary>The written bytes as a span.</summary>
    public ReadOnlySpan<byte> WrittenSpan => buffer.AsSpan(0, WrittenCount);

    /// <summary>Unused capacity remaining before the next grow.</summary>
    public int FreeCapacity => buffer.Length - WrittenCount;

    /// <summary>Rents an initial buffer of at least <paramref name="sizeHint"/> bytes.</summary>
    /// <param name="growFactor">Fraction to grow by when out of space.</param>
    /// <param name="maxAllowedSize">Hard ceiling; growing past it throws.</param>
    public PooledArrayBufferWriter(int sizeHint, float growFactor, int maxAllowedSize)
    {
        Expect.Range(sizeHint, 0, int.MaxValue);
        Expect.Range(growFactor, 0.1f, 100f);

        this.growFactor = growFactor;
        this.maxAllowedSize = maxAllowedSize;
        buffer = ArrayPool<byte>.Shared.Rent(sizeHint);
        WrittenCount = 0;
    }

    /// <summary>Returns the rented array to the pool.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ArrayPool<byte>.Shared.Return(buffer);
        WrittenCount = 0;
    }

    /// <summary>Marks <paramref name="count"/> bytes of the last requested span as written.</summary>
    public void Advance(int count)
    {
        if (count < 0 || WrittenCount > buffer.Length - count)
        {
            throw new InvalidOperationException();
        }

        WrittenCount += count;
    }

    /// <summary>Returns writable memory of at least <paramref name="sizeHint"/> bytes, growing the buffer if needed.</summary>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureBigEnoughBuffer(sizeHint);
        return buffer.AsMemory(WrittenCount);
    }

    /// <summary>Returns a writable span of at least <paramref name="sizeHint"/> bytes, growing the buffer if needed.</summary>
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureBigEnoughBuffer(sizeHint);
        return buffer.AsSpan(WrittenCount);
    }

    /// <summary>Resets the writer to empty, keeping a buffer of the same capacity for reuse.</summary>
    public void Clear()
    {
        var length = buffer.Length;
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = ArrayPool<byte>.Shared.Rent(length);
        WrittenCount = 0;
    }

    internal Memory<byte> GetColumnHeaderMemory()
        => buffer.AsMemory(0, ColumnHeader.Size);

    private void EnsureBigEnoughBuffer(int sizeHint)
    {
        if (sizeHint <= FreeCapacity)
        {
            return;
        }

        var minimumNeeded = sizeHint + WrittenCount;
        var bufferGrownByFactor = Math.Ceiling(buffer.Length * (1 + growFactor));
        var newSize = (int)Math.Max(minimumNeeded, bufferGrownByFactor);
        if (newSize > maxAllowedSize || newSize < 1)
        {
            throw new InvalidOperationException("It is prohibited to rent buffer of this size.");
        }

        var temp = ArrayPool<byte>.Shared.Rent(newSize);
        Array.Copy(buffer, temp, WrittenCount);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = temp;
        ++ResizedCount;
    }
}
