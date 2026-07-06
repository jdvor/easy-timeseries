namespace Easy.TimeSeries.TestData;

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

    [Column(0, Label = "country")]
    public string CountryCode { get; set; } = string.Empty;

    [Column(1, Label = "capacity_mw", NumberDistribution = NumberDistribution.Random)]
    public float CapacityMw { get; set; }

    [Column(2, Label = "latitude", NumberDistribution = NumberDistribution.Random)]
    public float Latitude { get; set; }

    [Column(3, Label = "longitude", NumberDistribution = NumberDistribution.Random)]
    public float Longitude { get; set; }

    [Column(4, Label = "primary_fuel")]
    public string PrimaryFuel { get; set; } = string.Empty;
}
