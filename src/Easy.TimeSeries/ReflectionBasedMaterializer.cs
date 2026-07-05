namespace Easy.TimeSeries;

using Easy.TimeSeries.Abstractions;
using System.Collections.Immutable;

/// <summary>
/// Reference materializer implementation backed by reflection. Intended for small data sets,
/// tests, and as a baseline for the source-generated materializers. Setters are compiled to
/// delegates at type-cache build time to avoid the per-call allocation of
/// <see cref="System.Reflection.MethodBase.Invoke(object?, object?[])"/>.
/// </summary>
public sealed class ReflectionBasedMaterializer<T> : IMaterializer<T>
    where T : class, new()
{
    private readonly ImmutableDictionary<int, TypeCache.ColumnBinding> bindings;
    private T[]? rows;
    private TypeCache.ColumnBinding? currentBinding;
    private int currentRow;

    public ReflectionBasedMaterializer()
    {
        bindings = TypeCache.Get<T>();
    }

    public bool BeginColumn(int column, int rowCount)
    {
        EnsureRowsOfSufficientSize(rowCount);
        var bindingExists = bindings.TryGetValue(column, out currentBinding);
        currentRow = 0;
        return bindingExists;
    }

    private void EnsureRowsOfSufficientSize(int rowCount)
    {
        if (rows is null)
        {
            rows = new T[rowCount];
            for (var i = 0; i < rowCount; i++)
            {
                rows[i] = new T();
            }
        }
        else if (rowCount > rows.Length + 1)
        {
            var resizedRows = new T[rowCount];
            Array.Copy(rows, resizedRows, rows.Length);
            for (var i = rows.Length; i < rowCount; i++)
            {
                rows[i] = new T();
            }

            rows = resizedRows;
        }
    }

    public void Hydrate<TPropValue>(TPropValue value)
        where TPropValue : struct
    {
        if (rows is null || currentBinding is null)
        {
            throw new InvalidOperationException("BeginColumn must be called before Hydrate.");
        }

        var instance = rows[currentRow++];
        currentBinding.Setter(instance, value);
    }

    public void Hydrate(string value)
    {
        if (rows is null || currentBinding is null)
        {
            throw new InvalidOperationException("BeginColumn must be called before Hydrate.");
        }

        var instance = rows[currentRow++];
        currentBinding.Setter(instance, value);
    }

    public T[] GetResult() => rows ?? [];
}
