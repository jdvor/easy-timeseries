namespace Easy.TimeSeries.Aggregation;

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

/// <summary>
/// Column buffers of one chunk as seen by the slices: one typed array per input slot, plus the timestamps. Holds the
/// per-slot sorted scratch that <see cref="AggregationSlice.Sorted{T}"/> shares across functions within a slice.
/// </summary>
internal sealed class SliceData : IDisposable
{
    private readonly Array?[] sorted;
    private readonly int[] sortedLength;
    private readonly long[] sortedVersion;
    private long version = 1;

    public SliceData(Type[] types)
    {
        Types = types;
        Columns = new Array[types.Length];
        sorted = new Array?[types.Length];
        sortedLength = new int[types.Length];
        sortedVersion = new long[types.Length];
    }

    /// <summary>Decoded CLR type per slot.</summary>
    public Type[] Types { get; }

    /// <summary>One array per slot, of <see cref="Types"/> element type, at least as long as the chunk.</summary>
    public Array[] Columns { get; }

    /// <summary>Timestamps of the chunk's rows.</summary>
    public DateTime[] Times { get; set; } = [];

    /// <summary>Invalidates the sorted copies; called before each slice.</summary>
    public void NextSlice() => ++version;

    public ReadOnlySpan<T> Sorted<T>(int slot, int offset, int count)
        where T : unmanaged, INumber<T>
    {
        if (sortedVersion[slot] == version)
        {
            return new ReadOnlySpan<T>(Unsafe.As<T[]>(sorted[slot]), 0, sortedLength[slot]);
        }

        var scratch = sorted[slot] as T[];
        if (scratch is null || scratch.Length < count)
        {
            if (scratch is not null)
            {
                ArrayPool<T>.Shared.Return(scratch);
            }

            scratch = ArrayPool<T>.Shared.Rent(count);
            sorted[slot] = scratch;
        }

        var source = new ReadOnlySpan<T>(Unsafe.As<T[]>(Columns[slot]), offset, count);
        var length = 0;
        foreach (var value in source)
        {
            if (!T.IsNaN(value))
            {
                scratch[length++] = value;
            }
        }

        var span = scratch.AsSpan(0, length);
        span.Sort();
        sortedLength[slot] = length;
        sortedVersion[slot] = version;
        return span;
    }

    public void Dispose()
    {
        for (var slot = 0; slot < sorted.Length; slot++)
        {
            switch (sorted[slot])
            {
                case float[] a: ArrayPool<float>.Shared.Return(a); break;
                case double[] a: ArrayPool<double>.Shared.Return(a); break;
                case decimal[] a: ArrayPool<decimal>.Shared.Return(a); break;
                case int[] a: ArrayPool<int>.Shared.Return(a); break;
                case long[] a: ArrayPool<long>.Shared.Return(a); break;
            }

            sorted[slot] = null;
        }
    }
}
