namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Receives decoded column values from the reader and assembles them into instances of <typeparamref name="T"/>.
/// The reader drives it one column at a time: it calls <see cref="BeginColumn"/>, then <c>Hydrate</c> once per row,
/// and finally <see cref="GetResult"/>. This is the seam between the format-level reader and a concrete DTO; the
/// reflection-based and source-generated materializers are the two shipped implementations.
/// </summary>
/// <typeparam name="T">The row type being materialized.</typeparam>
public interface IMaterializer<out T>
    where T : class, new()
{
    /// <summary>
    /// Announces the column that is about to be read and how many rows it holds. Return <c>false</c> to signal the
    /// column is not mapped to any property, letting the reader skip it.
    /// </summary>
    bool BeginColumn(int column, int rowCount);

    /// <summary>Sets the current column's value on the next row for value-typed columns.</summary>
    void Hydrate<TPropValue>(TPropValue value)
        where TPropValue : struct;

    /// <summary>Sets the current column's value on the next row for string (category) columns.</summary>
    void Hydrate(string value);

    /// <summary>Returns the fully materialized rows after all columns have been read.</summary>
    T[] GetResult();
}
