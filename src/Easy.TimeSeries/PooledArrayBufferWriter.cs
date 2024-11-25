namespace Easy.TimeSeries;

using System.Buffers;

public sealed class PooledArrayBufferWriter : IBufferWriter<byte>, IDisposable
{
    private readonly float growFactor;
    private readonly int maxAllowedSize;
    private byte[] buffer;
    private bool disposed;

    public int WrittenCount { get; private set; }

    internal int ResizedCount { get; private set; }

    public int Capacity => buffer.Length;

    public ReadOnlyMemory<byte> WrittenMemory => buffer.AsMemory(0, WrittenCount);

    public ReadOnlySpan<byte> WrittenSpan => buffer.AsSpan(0, WrittenCount);

    public int FreeCapacity => buffer.Length - WrittenCount;

    public PooledArrayBufferWriter(int sizeHint, float growFactor, int maxAllowedSize)
    {
        Expect.Range(sizeHint, 0, int.MaxValue);
        Expect.Range(growFactor, 0.1f, 100f);

        this.growFactor = growFactor;
        this.maxAllowedSize = maxAllowedSize;
        buffer = ArrayPool<byte>.Shared.Rent(sizeHint);
        WrittenCount = 0;
    }

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

    public void Advance(int count)
    {
        if (count < 0 || WrittenCount > buffer.Length - count)
        {
            throw new InvalidOperationException();
        }

        WrittenCount += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureBigEnoughBuffer(sizeHint);
        return buffer.AsMemory(WrittenCount);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureBigEnoughBuffer(sizeHint);
        return buffer.AsSpan(WrittenCount);
    }

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
