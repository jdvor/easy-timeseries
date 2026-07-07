namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Marks a DTO property as a stored column and controls how the source generator encodes it.
/// The <see cref="Index"/> fixes the column's position in the file and is the stable identity used when reading
/// back; the remaining options are hints that let you trade size against precision or match the encoding to how the
/// values are distributed. Only the option relevant to the property's type is consulted (e.g. <see cref="DateTimeSort"/>
/// applies to <see cref="System.DateTime"/> properties, <see cref="NumberPrecision"/> to floating-point ones).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute : Attribute
{
    /// <summary>Zero-based position of the column in the file. Must be unique within a DTO and is the identity used on read.</summary>
    public int Index { get; }

    /// <summary>Overrides the stored representation. <see cref="DataValueType.Auto"/> lets the generator pick from the property's CLR type.</summary>
    public DataValueType ValueType { get; set; } = DataValueType.Auto;

    /// <summary>Optional human-readable column name persisted in the file header. Defaults to the property name when empty.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Resolution a <see cref="System.DateTime"/> column is stored at. Coarser precision compresses better but truncates.</summary>
    public DateTimePrecision DateTimePrecision { get; set; } = DateTimePrecision.Milliseconds;

    /// <summary>Whether a <see cref="System.DateTime"/> column is monotonically ascending. Ascending enables the far more compact timestamp encoding.</summary>
    public DateTimeSort DateTimeSort { get; set; } = DateTimeSort.Unsorted;

    /// <summary>Resolution a <see cref="System.TimeSpan"/> column is stored at.</summary>
    public TimeSpanPrecision TimeSpanPrecision { get; set; } = TimeSpanPrecision.Seconds;

    /// <summary>Fixed number of decimal places to keep for a floating-point column. Enables lossy scaled-integer storage, which compresses dramatically better than raw floats.</summary>
    public NumberPrecision NumberPrecision { get; set; } = NumberPrecision.Auto;

    /// <summary>How the values of a numeric column are distributed, so the generator can pick between XOR/delta and raw+Brotli encoding.</summary>
    public NumberDistribution NumberDistribution { get; set; } = NumberDistribution.Continuous;

    /// <summary>Declares the property as column <paramref name="index"/>.</summary>
    /// <param name="index">Zero-based, file-stable column position.</param>
    public ColumnAttribute(int index)
    {
        Index = index;
    }
}
