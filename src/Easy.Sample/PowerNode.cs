namespace Easy.Sample;

using Easy.TimeSeries.Abstractions;

[GenerateReader]
[GenerateWriter]
public sealed class PowerNode
{
    [Column(0, Label = "Country")]
    public string CountryCode { get; set; } = string.Empty;

    [Column(1, Label = "Capacity (MW)", NumberPrecision = NumberPrecision.DecimalPlaces3)]
    public float CapacityMw { get; set; }

    [Column(2)]
    public float Latitude { get; set; }

    [Column(3)]
    public float Longitude { get; set; }

    [Column(4, Label = "Primary Fuel")]
    public string PrimaryFuel { get; set; } = string.Empty;

    [Column(5, Label = "Enabled")]
    public bool IsEnabled { get; set; }

    [Column(6, Label = "Start Duration")]
    public TimeSpan StartDuration { get; set; }

    [Column(7, Label = "Start Price")]
    public decimal StartPrice { get; set; }

    [Column(8, Label = "ID")]
    public long PowerNodeId { get; set; }

    /// <summary>
    /// always growing time
    /// </summary>
    [Column(9, Label = "Measurement Time", DateTimePrecision = DateTimePrecision.Milliseconds, DateTimeSort = DateTimeSort.Ascending)]
    public DateTime MeasurementTimeUtc { get; set; }

    /// <summary>
    /// random time, not ordered
    /// </summary>
    [Column(10, Label = "Certified Time", DateTimePrecision = DateTimePrecision.Days)]
    public DateTime CertifiedTimeUtc { get; set; }
}
