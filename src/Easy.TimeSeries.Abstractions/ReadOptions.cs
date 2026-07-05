namespace Easy.TimeSeries.Abstractions;

public sealed record ReadOptions(
    bool IgnoreUnknownColumnIndexes = true,
    int[]? ColumnIndexes = null)
{
}
