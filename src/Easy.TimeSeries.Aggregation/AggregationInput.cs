namespace Easy.TimeSeries.Aggregation;

/// <summary>
/// One raw column read by an aggregation function: by label or by column index, with the CLR type it decodes to.
/// </summary>
public sealed record AggregationInput
{
    private AggregationInput(string? label, int? columnIndex, Type valueType)
    {
        Label = label;
        ColumnIndex = columnIndex;
        ValueType = valueType;
    }

    /// <summary>Raw column label, or <c>null</c> when the input names a column index.</summary>
    public string? Label { get; }

    /// <summary>Raw column index, or <c>null</c> when the input names a label.</summary>
    public int? ColumnIndex { get; }

    /// <summary>
    /// Decoded CLR type: <see cref="float"/>, <see cref="double"/>, <see cref="decimal"/>, <see cref="int"/>,
    /// <see cref="long"/>, <see cref="DateTime"/>, <see cref="TimeSpan"/>, <see cref="bool"/> or <see cref="string"/>.
    /// </summary>
    public Type ValueType { get; }

    /// <summary>Input matched by raw column label.</summary>
    public static AggregationInput Of<T>(string label)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        return new AggregationInput(label, null, CheckType(typeof(T)));
    }

    /// <summary>Input matched by raw column index; the index must point to the same label in every raw file.</summary>
    public static AggregationInput Of<T>(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        return new AggregationInput(null, columnIndex, CheckType(typeof(T)));
    }

    /// <summary>Label, or <c>#index</c> for an index input; used in error messages.</summary>
    public override string ToString() => Label ?? $"#{ColumnIndex}";

    private static Type CheckType(Type type)
        => ClrTypes.IsSupported(type)
            ? type
            : throw new ArgumentException(
                $"'{type.Name}' is not a decoded column type. Use one of: {ClrTypes.SupportedNames}.",
                nameof(type));
}
