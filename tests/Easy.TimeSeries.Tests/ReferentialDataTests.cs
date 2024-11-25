namespace Easy.TimeSeries.Tests;

using Storage;

public class ReferentialDataTests
{
    [Fact]
    public async Task One()
    {
        var powerPlants = Data.GetPowerPlants();

        var countries = powerPlants.Select(x => x.CountryCode);
        var capacities = powerPlants.Select(x => x.CapacityMw);
        var latitudes = powerPlants.Select(x => x.Latitude);
        var longitudes = powerPlants.Select(x => x.Longitude);
        var fuels = powerPlants.Select(x => x.PrimaryFuel);

        var writer = new Writer(powerPlants.Count)
            .AddCategory(countries, "Country")
            .AddScaledNumber32(capacities, "Capacity MW", decimalPlaces: 2)
            .AddScaledNumber32(latitudes, "Latitude", decimalPlaces: 4)
            .AddScaledNumber32(longitudes, "Longitude", decimalPlaces: 4)
            .AddCategory(fuels, "Primary Fuel");

        var storage = new InMemoryStorage();
        await writer.WriteToAsync(storage);

        var hydrator = new ReflectionBasedDeserializer<PowerPlant>();
        var reader = new ReadBuilder();
        var result = await reader.ReadFromAsync(storage, hydrator);
    }
}
