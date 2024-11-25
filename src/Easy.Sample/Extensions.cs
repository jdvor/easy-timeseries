namespace Easy.Sample;

using Easy.TimeSeries;
using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Storage;
using System.Buffers;

public static class Extensions
{
    public static async Task WriteToAsync(
        this ICollection<PowerPlant> powerPlants,
        IWriteStorage storage,
        CancellationToken cancellationToken)
    {
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

        await writer.WriteToAsync(storage, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PowerPlantHydrator : IHydrator<PowerPlant>
{
    private readonly int rows;
    private readonly PowerPlant[] data;

    public PowerPlantHydrator(int rows)
    {
        this.rows = rows;
        data = new PowerPlant[rows];
    }

    public void Hydrate<TPropValue>(int column, int row, TPropValue value)
        where TPropValue : struct
    {
        throw new NotImplementedException();
    }

    public void Hydrate(int column, int row, string value)
    {
        throw new NotImplementedException();
    }

    public PowerPlant[] GetResult()
    {
        throw new NotImplementedException();
    }
}
