namespace Easy.TimeSeries.Aggregation.Engine;

using System.Buffers;

/// <summary>Renting, growing and permuting pooled arrays of the nine decoded types, dispatched by a closed switch.</summary>
internal static class PooledArrays
{
    public static Array Rent(Type type, int length)
    {
        if (type == typeof(float))
        {
            return ArrayPool<float>.Shared.Rent(length);
        }

        if (type == typeof(double))
        {
            return ArrayPool<double>.Shared.Rent(length);
        }

        if (type == typeof(decimal))
        {
            return ArrayPool<decimal>.Shared.Rent(length);
        }

        if (type == typeof(int))
        {
            return ArrayPool<int>.Shared.Rent(length);
        }

        if (type == typeof(long))
        {
            return ArrayPool<long>.Shared.Rent(length);
        }

        if (type == typeof(DateTime))
        {
            return ArrayPool<DateTime>.Shared.Rent(length);
        }

        if (type == typeof(TimeSpan))
        {
            return ArrayPool<TimeSpan>.Shared.Rent(length);
        }

        if (type == typeof(bool))
        {
            return ArrayPool<bool>.Shared.Rent(length);
        }

        return type == typeof(string)
            ? ArrayPool<string>.Shared.Rent(length)
            : throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported column type.");
    }

    public static void Return(Array? array)
    {
        switch (array)
        {
            case null: break;
            case float[] a: ArrayPool<float>.Shared.Return(a); break;
            case double[] a: ArrayPool<double>.Shared.Return(a); break;
            case decimal[] a: ArrayPool<decimal>.Shared.Return(a); break;
            case int[] a: ArrayPool<int>.Shared.Return(a); break;
            case long[] a: ArrayPool<long>.Shared.Return(a); break;
            case DateTime[] a: ArrayPool<DateTime>.Shared.Return(a); break;
            case TimeSpan[] a: ArrayPool<TimeSpan>.Shared.Return(a); break;
            case bool[] a: ArrayPool<bool>.Shared.Return(a); break;
            case string[] a: ArrayPool<string>.Shared.Return(a, clearArray: true); break;
            default: throw new ArgumentOutOfRangeException(nameof(array), array.GetType(), "Unsupported column type.");
        }
    }

    /// <summary>A larger array with the first <paramref name="keep"/> elements copied; the old one goes back to the pool.</summary>
    public static Array Grow(Array old, Type type, int length, int keep)
    {
        var grown = Rent(type, length);
        Array.Copy(old, grown, keep);
        Return(old);
        return grown;
    }

    /// <summary>A new array holding <c>source[order[i]]</c> at <c>i</c>; the source goes back to the pool.</summary>
    public static Array Permute(Array source, Type type, ReadOnlySpan<int> order)
    {
        var target = Rent(type, source.Length);
        switch (source)
        {
            case float[] a: Permute(a, (float[])target, order); break;
            case double[] a: Permute(a, (double[])target, order); break;
            case decimal[] a: Permute(a, (decimal[])target, order); break;
            case int[] a: Permute(a, (int[])target, order); break;
            case long[] a: Permute(a, (long[])target, order); break;
            case DateTime[] a: Permute(a, (DateTime[])target, order); break;
            case TimeSpan[] a: Permute(a, (TimeSpan[])target, order); break;
            case bool[] a: Permute(a, (bool[])target, order); break;
            case string[] a: Permute(a, (string[])target, order); break;
            default: throw new ArgumentOutOfRangeException(nameof(source), source.GetType(), "Unsupported column type.");
        }

        Return(source);
        return target;
    }

    private static void Permute<T>(T[] source, T[] target, ReadOnlySpan<int> order)
    {
        for (var i = 0; i < order.Length; i++)
        {
            target[i] = source[order[i]];
        }
    }
}

/// <summary>An append-only pooled buffer.</summary>
internal sealed class GrowableBuffer<T> : IDisposable
{
    private T[] items = ArrayPool<T>.Shared.Rent(256);

    public int Count { get; private set; }

    public T[] Items => items;

    public ArraySegment<T> Segment => new(items, 0, Count);

    public void Add(T item)
    {
        if (Count == items.Length)
        {
            var grown = ArrayPool<T>.Shared.Rent(items.Length * 2);
            Array.Copy(items, grown, Count);
            ArrayPool<T>.Shared.Return(items, clearArray: !typeof(T).IsValueType);
            items = grown;
        }

        items[Count++] = item;
    }

    public void Dispose()
    {
        ArrayPool<T>.Shared.Return(items, clearArray: !typeof(T).IsValueType);
        items = [];
        Count = 0;
    }
}
