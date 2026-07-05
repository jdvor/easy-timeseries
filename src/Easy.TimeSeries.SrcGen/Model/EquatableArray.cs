namespace Easy.TimeSeries.SrcGen.Model;

using System.Collections;

/// <summary>
/// Immutable array wrapper with sequence-based equality, required for incremental
/// generator models so the pipeline can cache on value equality.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly T[]? items;

    public EquatableArray(T[] items)
    {
        this.items = items;
    }

    public int Count => items?.Length ?? 0;

    public T this[int index] => items![index];

    public bool Equals(EquatableArray<T> other)
    {
        if (ReferenceEquals(items, other.items))
        {
            return true;
        }

        if (items is null || other.items is null || items.Length != other.items.Length)
        {
            return false;
        }

        for (var i = 0; i < items.Length; i++)
        {
            if (!items[i].Equals(other.items[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (items is null)
        {
            return 0;
        }

        unchecked
        {
            var hash = 17;
            foreach (var item in items)
            {
                hash = (hash * 31) + item.GetHashCode();
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(items ?? [])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
