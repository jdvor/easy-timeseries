namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Resolution a <see cref="System.DateTime"/> column is stored at. Timestamps are truncated to this unit before
/// encoding, so coarser precision loses sub-unit detail but compresses better. Pick the coarsest unit your data
/// tolerates.
/// </summary>
public enum DateTimePrecision
{
    /// <summary>Millisecond resolution.</summary>
    Milliseconds,

    /// <summary>100-millisecond resolution.</summary>
    TenthsOfSecond,

    /// <summary>Second resolution.</summary>
    Seconds,

    /// <summary>Day resolution.</summary>
    Days,

    /// <summary>Year resolution.</summary>
    Years,
}
