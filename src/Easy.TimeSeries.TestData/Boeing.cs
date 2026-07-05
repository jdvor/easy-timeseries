namespace Easy.TimeSeries.TestData;

using Easy.TimeSeries.Abstractions;

[GenerateReader]
[GenerateWriter]
public sealed class Boeing
{
    [Column(0, Label = "year")]
    public int Year { get; set; }

    [Column(1, Label = "month")]
    public int Month { get; set; }

    [Column(2, Label = "day")]
    public int Day { get; set; }

    [Column(3, Label = "hour")]
    public int Hour { get; set; }

    [Column(4, Label = "minute")]
    public int Minute { get; set; }

    [Column(5, Label = "second")]
    public int Second { get; set; }

    [Column(6, Label = "price", NumberPrecision = NumberPrecision.DecimalPlaces2)]
    public float Price { get; set; }

    [Column(7, Label = "volume")]
    public int Volume { get; set; }

    [Column(8, Label = "time", DateTimePrecision = DateTimePrecision.Seconds, DateTimeSort = DateTimeSort.Ascending)]
    public DateTime Time { get; set; }
}
