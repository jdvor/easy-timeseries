namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Controls how a buffer is read back.
/// </summary>
/// <param name="IgnoreUnknownColumnIndexes">
/// When <c>true</c>, columns present in the file but not mapped by the target DTO are silently skipped; when
/// <c>false</c>, encountering such a column throws.
/// </param>
/// <param name="ColumnIndexes">
/// When set, only these column indexes are decoded (projection), in the given order. <c>null</c> reads every column.
/// </param>
public sealed record ReadOptions(
    bool IgnoreUnknownColumnIndexes = true,
    int[]? ColumnIndexes = null)
{
}
