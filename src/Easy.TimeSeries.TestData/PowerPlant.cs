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

    [Column(0)]
    public string CountryCode { get; set; } = string.Empty;

    [Column(1)]
    public float CapacityMw { get; set; }

    [Column(2)]
    public float Latitude { get; set; }

    [Column(3)]
    public float Longitude { get; set; }

    [Column(4)]
    public string PrimaryFuel { get; set; } = string.Empty;
}
