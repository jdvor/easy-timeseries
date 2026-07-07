namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Declares whether a <see cref="System.DateTime"/> column's values are ordered, which selects the timestamp
/// encoding. <see cref="Ascending"/> unlocks delta-of-delta encoding and is far more compact; use it only when the
/// values are genuinely monotonically non-decreasing, otherwise writing throws.
/// </summary>
public enum DateTimeSort
{
    /// <summary>Values may appear in any order. Stored as absolute XOR-encoded timestamps.</summary>
    Unsorted,

    /// <summary>Values are monotonically non-decreasing. Stored with the compact delta-of-delta encoding.</summary>
    Ascending,
}
