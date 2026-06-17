namespace Easy.Sample;

using Easy.TimeSeries.Abstractions;

[GenerateReader]
[GenerateWriter]
public sealed class PowerPlant
{
    public PowerPlant()
    {
    }

    public PowerPlant(string countryCode, float capacityMw, float latitude, float longitude, string primaryFuel)
    {
        CountryCode = countryCode;
        CapacityMw = capacityMw;
        Latitude = latitude;
        Longitude = longitude;
        PrimaryFuel = primaryFuel;
    }

    [Column(1, Label = "Country")]
    public string CountryCode { get; set; } = string.Empty;

    [Column(2, Label = "Capacity (MW)", NumberPrecision = NumberPrecision.DecimalPlaces2)]
    public float CapacityMw { get; set; }

    [Column(3, NumberPrecision = NumberPrecision.DecimalPlaces4)]
    public float Latitude { get; set; }

    [Column(4, NumberPrecision = NumberPrecision.DecimalPlaces4)]
    public float Longitude { get; set; }

    [Column(5, Label = "Primary Fuel")]
    public string PrimaryFuel { get; set; } = string.Empty;
}
