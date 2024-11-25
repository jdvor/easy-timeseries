namespace Easy.TimeSeries.Tests;

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

    public string CountryCode { get; set; } = string.Empty;

    public float CapacityMw { get; set; }

    public float Latitude { get; set; }

    public float Longitude { get; set; }

    public string PrimaryFuel { get; set; } = string.Empty;
}
