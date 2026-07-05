namespace Easy.TimeSeries.TestData;

using Easy.TimeSeries.Abstractions;

[GenerateReader]
[GenerateWriter]
public sealed class Vix
{
    [Column(0, Label = "Date", DateTimePrecision = DateTimePrecision.Days, DateTimeSort = DateTimeSort.Ascending)]
    public DateTime Date { get; set; }

    [Column(1, Label = "VIX Open", NumberPrecision = NumberPrecision.DecimalPlaces2)]
    public float Open { get; set; }

    [Column(2, Label = "VIX High", NumberPrecision = NumberPrecision.DecimalPlaces2)]
    public float High { get; set; }

    [Column(3, Label = "VIX Low", NumberPrecision = NumberPrecision.DecimalPlaces2)]
    public float Low { get; set; }

    [Column(4, Label = "VIX Close", NumberPrecision = NumberPrecision.DecimalPlaces2)]
    public float Close { get; set; }
}
