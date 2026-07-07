namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Fixed number of decimal places to keep for a floating-point column. Setting an explicit precision switches the
/// column to lossy scaled-integer storage (values are rounded to that many places), which compresses dramatically
/// better than raw floats. Use it when the meaningful precision of the data is a small, known number of decimals.
/// </summary>
public enum NumberPrecision
{
    /// <summary>Keep full floating-point precision (lossless). No scaled-integer conversion.</summary>
    Auto = 0,

    /// <summary>Round to 1 decimal place.</summary>
    DecimalPlaces1 = 1,

    /// <summary>Round to 2 decimal places.</summary>
    DecimalPlaces2 = 2,

    /// <summary>Round to 3 decimal places.</summary>
    DecimalPlaces3 = 3,

    /// <summary>Round to 4 decimal places.</summary>
    DecimalPlaces4 = 4,

    /// <summary>Round to 5 decimal places.</summary>
    DecimalPlaces5 = 5,
}
