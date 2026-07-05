namespace Easy.TimeSeries.Abstractions;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "because it is type name")]
public enum DataValueType
{
    Auto = 0,
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
}
