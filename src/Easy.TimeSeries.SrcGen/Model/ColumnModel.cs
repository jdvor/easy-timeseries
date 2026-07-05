namespace Easy.TimeSeries.SrcGen.Model;

internal enum GenerationKind
{
    Writer,
    Reader,
}

/// <summary>
/// Generator-local mirror of <c>Easy.TimeSeries.ColumnValueType</c> (values 1..12 match
/// <c>Easy.TimeSeries.Abstractions.DataValueType</c>); duplicated so the generator assembly
/// has no runtime dependency on either library.
/// </summary>
internal enum ColumnKind
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
}

/// <summary>
/// One [Column] property bound to its storage mapping. TimePrecisionName is a member name of
/// <c>Easy.TimeSeries.TimePrecision</c> (empty for non-time columns); DecimalPlaces is 0 for
/// non-scaled columns.
/// </summary>
internal readonly record struct ColumnModel(
    int Index,
    string PropertyName,
    ColumnKind Kind,
    string Label,
    string TimePrecisionName,
    int DecimalPlaces);
