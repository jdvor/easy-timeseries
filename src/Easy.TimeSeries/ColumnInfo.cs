namespace Easy.TimeSeries;

public readonly record struct ColumnInfo
{
    public const int MaxLabelLength = 120;

    public static readonly ColumnInfo Empty = new(0);

    public int Index { get; }

    public ColumnValueType ValueType { get; }

    public int Meta { get; }

    public string Label { get; }

    public bool IsEmpty => ValueType == ColumnValueType.None;

    // value type (1), meta (4), label string length (1), utf8 label (worst case: n * 2)
    public int SizeHint => 1 + sizeof(int) + 1 + (Label.Length * 2);

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
            ColumnValueType.DateTime => nameof(ColumnValueType.DateTime),
            ColumnValueType.TimeSpan => nameof(ColumnValueType.TimeSpan),
            ColumnValueType.Float => nameof(ColumnValueType.Float),
            ColumnValueType.Double => nameof(ColumnValueType.Double),
            ColumnValueType.Int32 => nameof(ColumnValueType.Int32),
            ColumnValueType.Int64 => nameof(ColumnValueType.Int64),
            ColumnValueType.Bool => nameof(ColumnValueType.Bool),
            ColumnValueType.ScaledNumber32 => nameof(ColumnValueType.ScaledNumber32),
            ColumnValueType.ScaledNumber64 => nameof(ColumnValueType.ScaledNumber64),
            ColumnValueType.Category => nameof(ColumnValueType.Category),
            _ => "?",
        };
        return string.IsNullOrEmpty(Label)
            ? typeName
            : $"[{Index}] {typeName} ({Meta}) {Label}";
    }
}
