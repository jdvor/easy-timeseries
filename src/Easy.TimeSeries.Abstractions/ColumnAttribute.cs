namespace Easy.TimeSeries.Abstractions;

[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute : Attribute
{
    public int Index { get; }

    public DataValueType ValueType { get; set; } = DataValueType.Auto;

    public string Label { get; set; } = string.Empty;

    public DateTimePrecision DateTimePrecision { get; set; } = DateTimePrecision.Milliseconds;

    public DateTimeSort DateTimeSort { get; set; } = DateTimeSort.Unsorted;

    public TimeSpanPrecision TimeSpanPrecision { get; set; } = TimeSpanPrecision.Seconds;

    public NumberPrecision NumberPrecision { get; set; } = NumberPrecision.Auto;

    public ColumnAttribute(int index)
    {
        Index = index;
    }
}
