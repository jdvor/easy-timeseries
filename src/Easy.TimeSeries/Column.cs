namespace Easy.TimeSeries;

public readonly record struct Column(ColumnInfo Info, int Start, int Length);
