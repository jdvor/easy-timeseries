namespace Easy.TimeSeries.Aggregation;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// On-disk encoding of an aggregated column: a concrete <see cref="ColumnValueType"/> with its meta (decimal places
/// or time precision), or one of the two inherited encodings resolved from the raw column when the spec is bound.
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "mirrors ColumnValueType")]
public readonly record struct OutputEncoding
{
    private readonly Inheritance inheritance;

    private OutputEncoding(ColumnValueType valueType, int meta, Inheritance inheritance = Inheritance.None)
    {
        ValueType = valueType;
        Meta = meta;
        this.inheritance = inheritance;
    }

    private enum Inheritance : byte
    {
        None,
        SameAsInput,
        FromInputPrecision,
    }

    /// <summary>Concrete column type; <see cref="ColumnValueType.None"/> for an inherited encoding.</summary>
    public ColumnValueType ValueType { get; }

    /// <summary>Decimal places for scaled types, <see cref="TimePrecision"/> for time types, 0 otherwise.</summary>
    public int Meta { get; }

    /// <summary>Keep the raw column encoding as is; DateTimeOrdered becomes DateTimeUnordered.</summary>
    public static OutputEncoding SameAsInput { get; } = new(ColumnValueType.None, 0, Inheritance.SameAsInput);

    /// <summary>
    /// Scaled types keep type and decimals, Decimal stays Decimal, anything else is the lossless type of the result.
    /// </summary>
    public static OutputEncoding FromInputPrecision { get; } = new(ColumnValueType.None, 0, Inheritance.FromInputPrecision);

    /// <summary>Whether the encoding is resolved from the raw column when the spec is bound.</summary>
    public bool IsInherited => inheritance != Inheritance.None;

    internal bool IsSameAsInput => inheritance == Inheritance.SameAsInput;

    public static OutputEncoding Float { get; } = new(ColumnValueType.Float, 0);

    public static OutputEncoding FloatRaw { get; } = new(ColumnValueType.FloatRaw, 0);

    public static OutputEncoding Double { get; } = new(ColumnValueType.Double, 0);

    public static OutputEncoding DoubleRaw { get; } = new(ColumnValueType.DoubleRaw, 0);

    public static OutputEncoding Decimal { get; } = new(ColumnValueType.Decimal, 0);

    public static OutputEncoding Int32 { get; } = new(ColumnValueType.Int32, 0);

    public static OutputEncoding Int32Raw { get; } = new(ColumnValueType.Int32Raw, 0);

    public static OutputEncoding Int64 { get; } = new(ColumnValueType.Int64, 0);

    public static OutputEncoding Int64Raw { get; } = new(ColumnValueType.Int64Raw, 0);

    public static OutputEncoding Bool { get; } = new(ColumnValueType.Bool, 0);

    public static OutputEncoding Category { get; } = new(ColumnValueType.Category, 0);

    /// <param name="decimalPlaces">Decimal places to retain, 1-8.</param>
    public static OutputEncoding ScaledNumber32(int decimalPlaces)
        => new(ColumnValueType.ScaledNumber32, CheckDecimalPlaces(decimalPlaces));

    /// <param name="decimalPlaces">Decimal places to retain, 1-8.</param>
    public static OutputEncoding ScaledNumber64(int decimalPlaces)
        => new(ColumnValueType.ScaledNumber64, CheckDecimalPlaces(decimalPlaces));

    public static OutputEncoding DateTimeUnordered(TimePrecision precision)
        => new(ColumnValueType.DateTimeUnordered, (int)CheckPrecision(precision));

    public static OutputEncoding TimeSpan(TimePrecision precision)
        => new(ColumnValueType.TimeSpan, (int)CheckPrecision(precision));

    /// <summary>A concrete encoding equal to the raw column's own, used when resolving inherited encodings.</summary>
    internal static OutputEncoding Of(ColumnValueType valueType, int meta) => new(valueType, meta);

    public override string ToString()
        => inheritance switch
        {
            Inheritance.SameAsInput => nameof(SameAsInput),
            Inheritance.FromInputPrecision => nameof(FromInputPrecision),
            _ => Meta == 0 ? ValueType.ToString() : $"{ValueType}({Meta})",
        };

    private static int CheckDecimalPlaces(int decimalPlaces)
        => decimalPlaces is >= 1 and <= 8
            ? decimalPlaces
            : throw new ArgumentOutOfRangeException(nameof(decimalPlaces), decimalPlaces, "Decimal places must be 1-8.");

    private static TimePrecision CheckPrecision(TimePrecision precision)
        => Enum.IsDefined(precision)
            ? precision
            : throw new ArgumentOutOfRangeException(nameof(precision), precision, "Unknown time precision.");
}
