namespace Easy.TimeSeries;

/// <summary>
/// Resolution a timestamp or interval column is stored at, at the core-library level. Values are truncated to this
/// unit before encoding; coarser units compress better but drop finer detail. The public
/// <see cref="Abstractions.DateTimePrecision"/> and <see cref="Abstractions.TimeSpanPrecision"/> map onto this.
/// </summary>
public enum TimePrecision
{
    /// <summary>Millisecond resolution.</summary>
    Milliseconds,

    /// <summary>100-millisecond resolution.</summary>
    TenthsOfSecond,

    /// <summary>Second resolution.</summary>
    Seconds,

    /// <summary>Day resolution.</summary>
    Days,
}
