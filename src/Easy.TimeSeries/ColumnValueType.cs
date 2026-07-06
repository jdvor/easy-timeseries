namespace Easy.TimeSeries;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "because it is type name")]
public enum ColumnValueType : byte
{
    None = 0,
    DateTimeOrdered = 1,
    TimeSpan = 2,
    Float = 3,
    Double = 4,
    Decimal = 5,
    Int32 = 6,
    Int64 = 7,
    Bool = 8,
    ScaledNumber32 = 9,
    ScaledNumber64 = 10,
    Category = 11,
    DateTimeUnordered = 12,

    /// <summary>Raw little-endian <see cref="float"/> values, Brotli-compressed. For uncorrelated data.</summary>
    FloatRaw = 13,

    /// <summary>Raw little-endian <see cref="double"/> values, Brotli-compressed. For uncorrelated data.</summary>
    DoubleRaw = 14,

    /// <summary>Raw little-endian <see cref="long"/> values, Brotli-compressed. For uncorrelated data.</summary>
    Int64Raw = 15,

    /// <summary>Raw little-endian <see cref="int"/> values, Brotli-compressed. For uncorrelated data.</summary>
    Int32Raw = 16,
}
