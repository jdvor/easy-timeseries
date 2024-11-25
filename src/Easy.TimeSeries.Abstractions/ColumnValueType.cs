namespace Easy.TimeSeries.Abstractions;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "because it is type name")]
public enum ColumnValueType
{
    Auto = 0,
    DateTime = 1,
    TimeSpan = 2,
    Float = 3,
    Double = 4,
    Int32 = 5,
    Int64 = 6,
    Bool = 7,
    ScaledNumber32 = 8,
    ScaledNumber64 = 9,
    Category = 10,
}
