namespace Easy.TimeSeries.TestData;

using Easy.TimeSeries.Abstractions;

[GenerateReader]
[GenerateWriter]
public sealed class Gold
{
    [Column(0, DateTimePrecision = DateTimePrecision.Days, DateTimeSort = DateTimeSort.Ascending)]
    public DateTime Date { get; set; }

    [Column(1, NumberPrecision = NumberPrecision.DecimalPlaces2)]
    public float Value { get; set; }
}
