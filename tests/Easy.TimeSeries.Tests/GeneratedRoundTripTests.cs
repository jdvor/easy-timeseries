namespace Easy.TimeSeries.Tests;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Storage;
using TestData;

/// <summary>
/// End-to-end tests for the source-generated <see cref="PowerPlantWriter"/>,
/// <see cref="PowerPlantMaterializer"/> and <see cref="PowerPlantReader"/> (emitted by
/// Easy.TimeSeries.SrcGen into the Easy.TimeSeries.TestData assembly).
/// </summary>
public class GeneratedRoundTripTests
{
    [Fact]
    public async Task Generated_writer_and_reader_round_trip_power_plants()
    {
        var ct = TestContext.Current.CancellationToken;
        var powerPlants = Data.GetPowerPlants();

        using var storage = new InMemoryStorage();
        await new PowerPlantWriter().WriteAsync(powerPlants, storage, ct);
        var result = await new PowerPlantReader().ReadAsync(new ReadOptions(), storage, ct);

        Assert.Equal(powerPlants.Count, result.Length);
        for (var i = 0; i < powerPlants.Count; i++)
        {
            var expected = powerPlants[i];
            var actual = result[i];
            Assert.Equal(expected.CountryCode, actual.CountryCode);
            Assert.Equal(expected.CapacityMw, actual.CapacityMw);
            Assert.Equal(expected.Latitude, actual.Latitude);
            Assert.Equal(expected.Longitude, actual.Longitude);
            Assert.Equal(expected.PrimaryFuel, actual.PrimaryFuel);
        }
    }

    [Fact]
    public async Task Generated_materializer_matches_reflection_based_materializer()
    {
        var ct = TestContext.Current.CancellationToken;
        var powerPlants = Data.GetPowerPlants();

        using var storage = new InMemoryStorage();
        await new PowerPlantWriter().WriteAsync(powerPlants, storage, ct);

        var generated = await Reader.ReadFromAsync(storage, new PowerPlantMaterializer(), null, ct);
        var reflected = await Reader.ReadFromAsync(storage, new ReflectionBasedMaterializer<PowerPlant>(), null, ct);

        Assert.Equal(reflected.Length, generated.Length);
        for (var i = 0; i < reflected.Length; i++)
        {
            Assert.Equal(reflected[i].CountryCode, generated[i].CountryCode);
            Assert.Equal(reflected[i].CapacityMw, generated[i].CapacityMw);
            Assert.Equal(reflected[i].Latitude, generated[i].Latitude);
            Assert.Equal(reflected[i].Longitude, generated[i].Longitude);
            Assert.Equal(reflected[i].PrimaryFuel, generated[i].PrimaryFuel);
        }
    }
}
