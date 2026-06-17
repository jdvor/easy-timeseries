namespace Easy.TimeSeries;

using Easy.TimeSeries.Abstractions;
using System.Collections.Immutable;

/// <summary>
/// Reference hydrator implementation backed by reflection. Intended for small data sets,
/// tests, and as a baseline for the source-generated hydrators. Setters are compiled to
/// delegates at type-cache build time to avoid the per-call allocation of
/// <see cref="System.Reflection.MethodBase.Invoke(object?, object?[])"/>.
/// </summary>
public sealed class ReflectionBasedHydrator<T> : IHydrator<T>
    where T : class, new()
{
    private readonly ImmutableDictionary<int, TypeCache.ColumnBinding> bindings;
    private readonly List<T> rows = new(capacity: 16);
    private int lastColumn = -1;
    private TypeCache.ColumnBinding? lastBinding;

    public ReflectionBasedHydrator()
    {
        bindings = TypeCache.Get<T>();
    }

    public void Hydrate<TPropValue>(int column, int row, TPropValue value)
        where TPropValue : struct
        => Set(column, row, value);

    public void Hydrate(int column, int row, string value)
        => Set(column, row, value);

    public T[] GetResult() => rows.ToArray();

    private void Set(int column, int row, object? value)
    {
        var binding = ResolveBinding(column);
        if (binding is null)
        {
            return;
        }

        var instance = EnsureRow(row);
        binding.Setter(instance, value);
    }

    private TypeCache.ColumnBinding? ResolveBinding(int column)
    {
        if (column == lastColumn)
        {
            return lastBinding;
        }

        bindings.TryGetValue(column, out var binding);
        lastColumn = column;
        lastBinding = binding;
        return binding;
    }

    private T EnsureRow(int row)
    {
        while (rows.Count <= row)
        {
            rows.Add(new T());
        }

        return rows[row];
    }
}
