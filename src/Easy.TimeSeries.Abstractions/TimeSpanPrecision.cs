namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Resolution a <see cref="System.TimeSpan"/> column is stored at. Intervals are truncated to this unit before
/// encoding, so coarser precision loses sub-unit detail but compresses better.
/// </summary>
public enum TimeSpanPrecision
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
