namespace Easy.TimeSeries.Abstractions;

[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute : Attribute
{
    public ColumnValueType ValueType { get; set; } = ColumnValueType.Auto;

    public string Label { get; set; } = string.Empty;

    public DateTimePrecision DateTimePrecision { get; set; } = DateTimePrecision.Milliseconds;

    public TimeSpanPrecision TimeSpanPrecision { get; set; } = TimeSpanPrecision.Seconds;

    public NumberPrecision NumberPrecision { get; set; } = NumberPrecision.Auto;
}
