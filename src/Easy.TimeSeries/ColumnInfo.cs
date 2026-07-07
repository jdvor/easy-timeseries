namespace Easy.TimeSeries;

/// <summary>
/// Descriptor for one column as stored in the file header: its position, encoding, an encoding-specific
/// <see cref="Meta"/> payload, and an optional label. Together the descriptors let a reader locate and decode each
/// column block without inspecting the data.
/// </summary>
public readonly record struct ColumnInfo
{
    /// <summary>Maximum label length in characters.</summary>
    public const int MaxLabelLength = 120;

    /// <summary>Sentinel describing no column (value type <see cref="ColumnValueType.None"/>).</summary>
    public static readonly ColumnInfo Empty = new(0);

    /// <summary>Zero-based position of the column in the file.</summary>
    public int Index { get; }

    /// <summary>The column's on-disk encoding.</summary>
    public ColumnValueType ValueType { get; }

    /// <summary>Encoding-specific payload: time precision, decimal places, and so on. Interpretation depends on <see cref="ValueType"/>.</summary>
    public int Meta { get; }

    /// <summary>Optional human-readable column name; empty when unset.</summary>
    public string Label { get; }

    /// <summary>Whether this descriptor is the <see cref="Empty"/> sentinel.</summary>
    public bool IsEmpty => ValueType == ColumnValueType.None;

    // value type (1), meta (4), label string length (1), utf8 label (worst case: n * 2)
    /// <summary>Upper bound on the serialized size of this descriptor in bytes.</summary>
    public int SizeHint => 1 + sizeof(int) + 1 + (Label.Length * 2);

    /// <summary>Creates a descriptor for a column at <paramref name="index"/>.</summary>
    public ColumnInfo(int index, ColumnValueType valueType, int meta, string label)
    {
        Expect.Range(index, 0, Header.MaxColumns);
        if (label.Length > MaxLabelLength)
        {
            throw new ArgumentException($"Label length must not exceed {MaxLabelLength}.", nameof(label));
        }

        Index = index;
        ValueType = valueType;
        Meta = meta;
        Label = label;
    }

    private ColumnInfo(int meta)
    {
        ValueType = ColumnValueType.None;
        Meta = meta;
        Label = string.Empty;
    }

    public override string ToString()
    {
        var typeName = ValueType switch
        {
            ColumnValueType.DateTimeOrdered => nameof(ColumnValueType.DateTimeOrdered),
            ColumnValueType.TimeSpan => nameof(ColumnValueType.TimeSpan),
            ColumnValueType.Float => nameof(ColumnValueType.Float),
            ColumnValueType.Double => nameof(ColumnValueType.Double),
            ColumnValueType.Decimal => nameof(ColumnValueType.Decimal),
            ColumnValueType.Int32 => nameof(ColumnValueType.Int32),
            ColumnValueType.Int64 => nameof(ColumnValueType.Int64),
            ColumnValueType.Bool => nameof(ColumnValueType.Bool),
            ColumnValueType.ScaledNumber32 => nameof(ColumnValueType.ScaledNumber32),
            ColumnValueType.ScaledNumber64 => nameof(ColumnValueType.ScaledNumber64),
            ColumnValueType.Category => nameof(ColumnValueType.Category),
            ColumnValueType.DateTimeUnordered => nameof(ColumnValueType.DateTimeUnordered),
            ColumnValueType.FloatRaw => nameof(ColumnValueType.FloatRaw),
            ColumnValueType.DoubleRaw => nameof(ColumnValueType.DoubleRaw),
            ColumnValueType.Int64Raw => nameof(ColumnValueType.Int64Raw),
            ColumnValueType.Int32Raw => nameof(ColumnValueType.Int32Raw),
            _ => "?",
        };
        return string.IsNullOrEmpty(Label)
            ? typeName
            : $"[{Index}] {typeName} ({Meta}) {Label}";
    }
}
