namespace Easy.TimeSeries;

/// <summary>
/// A located column within a buffer: its <see cref="Info"/> descriptor plus the byte offset (<see cref="Start"/>)
/// and length of its block. Produced when a reader resolves the file layout, so a column can be sliced out and decoded.
/// </summary>
public readonly record struct Column(ColumnInfo Info, int Start, int Length);
