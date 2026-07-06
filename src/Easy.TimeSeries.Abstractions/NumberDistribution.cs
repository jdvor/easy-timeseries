namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Hints how the values of a floating-point or 64-bit integer column are distributed, so the writer
/// can pick a fitting encoding.
/// </summary>
public enum NumberDistribution
{
    /// <summary>
    /// Values change slowly relative to their neighbors (typical time-series signal). The column is
    /// XOR/delta ("Gorilla") encoded, which is compact only when successive values are similar.
    /// </summary>
    Continuous = 0,

    /// <summary>
    /// Values are effectively unrelated between rows (e.g. geographic coordinates, identifiers). XOR/delta
    /// encoding gives no benefit - or hurts - so the column is stored raw and Brotli-compressed instead.
    /// Applies to <see cref="float"/>, <see cref="double"/>, <see cref="long"/> and <see cref="int"/> columns only.
    /// </summary>
    Random = 1,
}
