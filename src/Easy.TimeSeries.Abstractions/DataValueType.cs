namespace Easy.TimeSeries.Abstractions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The stored representation a column is encoded as, chosen on a <see cref="ColumnAttribute"/>.
/// <see cref="Auto"/> defers the choice to the source generator, which maps the property's CLR type to a fitting
/// member. The raw+Brotli variants used for uncorrelated numbers are not selected here - set
/// <see cref="ColumnAttribute.NumberDistribution"/> to <see cref="NumberDistribution.Random"/> instead.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "because it is type name")]
public enum DataValueType
{
    /// <summary>Let the source generator pick the representation from the property's CLR type.</summary>
    Auto = 0,

    /// <summary>UTC timestamps stored in ascending order using delta-of-delta encoding.</summary>
    DateTimeOrdered = 1,

    /// <summary>Time intervals.</summary>
    TimeSpan = 2,

    /// <summary>32-bit IEEE floating point, XOR/delta encoded.</summary>
    Float = 3,

    /// <summary>64-bit IEEE floating point, XOR/delta encoded.</summary>
    Double = 4,

    /// <summary>128-bit .NET decimal, stored losslessly.</summary>
    Decimal = 5,

    /// <summary>32-bit signed integer, XOR/delta encoded.</summary>
    Int32 = 6,

    /// <summary>64-bit signed integer, XOR/delta encoded.</summary>
    Int64 = 7,

    /// <summary>Single-bit boolean.</summary>
    Bool = 8,

    /// <summary>Float quantized to a fixed number of decimal places and stored as a scaled 32-bit integer (lossy).</summary>
    ScaledNumber32 = 9,

    /// <summary>Double quantized to a fixed number of decimal places and stored as a scaled 64-bit integer (lossy).</summary>
    ScaledNumber64 = 10,

    /// <summary>Repeated strings stored as dictionary ids plus a label map.</summary>
    Category = 11,

    /// <summary>UTC timestamps in arbitrary order, stored as absolute XOR-encoded values.</summary>
    DateTimeUnordered = 12,
}
