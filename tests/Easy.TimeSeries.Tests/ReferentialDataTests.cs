namespace Easy.TimeSeries.Tests;

using Storage;

public class ReferentialDataTests
{
    [Fact]
    public async Task Write_and_read_power_plants_with_category_and_scaled_number()
    {
        var ct = TestContext.Current.CancellationToken;
        var powerPlants = Data.GetPowerPlants();

        var writer = new Writer(powerPlants.Count)
            .AddCategory(powerPlants.Select(x => x.CountryCode), "Country")
            .AddScaledNumber32(powerPlants.Select(x => x.CapacityMw), "Capacity MW", decimalPlaces: 2)
            .AddScaledNumber32(powerPlants.Select(x => x.Latitude), "Latitude", decimalPlaces: 4)
            .AddScaledNumber32(powerPlants.Select(x => x.Longitude), "Longitude", decimalPlaces: 4)
            .AddCategory(powerPlants.Select(x => x.PrimaryFuel), "Primary Fuel");

        var storage = new InMemoryStorage();
        await writer.WriteToAsync(storage, ct);

        var hydrator = new ReflectionBasedHydrator<PowerPlant>();
        var reader = new ReadBuilder();
        var result = await reader.ReadFromAsync(storage, hydrator, ct);

        Assert.Equal(powerPlants.Count, result.Length);
        for (var i = 0; i < powerPlants.Count; i++)
        {
            var expected = powerPlants[i];
            var actual = result[i];
            Assert.Equal(expected.CountryCode, actual.CountryCode);
            Assert.Equal(expected.PrimaryFuel, actual.PrimaryFuel);
            Assert.Equal(expected.CapacityMw, actual.CapacityMw, tolerance: 0.01f);
            Assert.Equal(expected.Latitude, actual.Latitude, tolerance: 0.0001f);
            Assert.Equal(expected.Longitude, actual.Longitude, tolerance: 0.0001f);
        }
    }

    [Fact]
    public async Task Write_and_read_dto_with_int32_float_and_double()
    {
        var ct = TestContext.Current.CancellationToken;
        var alphas = Alpha.DataSet1();

        var writer = new Writer(alphas.Length)
            .AddInt32(alphas.Select(x => x.Age), "Age")
            .AddFloat(alphas.Select(x => x.Radiation), "Radiation")
            .AddDouble(alphas.Select(x => x.TorqueRatio), "Torque");

        var storage = new InMemoryStorage();
        await writer.WriteToAsync(storage, ct);

        var hydrator = new ReflectionBasedHydrator<Alpha>();
        var reader = new ReadBuilder();
        var result = await reader.ReadFromAsync(storage, hydrator, ct);

        Assert.Equal(alphas.Length, result.Length);
        for (var i = 0; i < alphas.Length; i++)
        {
            Assert.Equal(alphas[i].Age, result[i].Age);
            Assert.Equal(alphas[i].Radiation, result[i].Radiation);
            Assert.Equal(alphas[i].TorqueRatio, result[i].TorqueRatio);
        }
    }

    [Fact]
    public async Task Write_and_read_dto_with_datetime_timespan_int64_bool()
    {
        var ct = TestContext.Current.CancellationToken;
        var betas = Beta.DataSet1();

        var writer = new Writer(betas.Length)
            .AddTime(betas.Select(x => x.Timestamp), "Timestamp")
            .AddInterval(betas.Select(x => x.Elapsed), "Elapsed")
            .AddInt64(betas.Select(x => x.Counter), "Counter")
            .AddBool(betas.Select(x => x.IsValid), "IsValid");

        var storage = new InMemoryStorage();
        await writer.WriteToAsync(storage, ct);

        var hydrator = new ReflectionBasedHydrator<Beta>();
        var reader = new ReadBuilder();
        var result = await reader.ReadFromAsync(storage, hydrator, ct);

        Assert.Equal(betas.Length, result.Length);
        for (var i = 0; i < betas.Length; i++)
        {
            Assert.Equal(betas[i].Timestamp, result[i].Timestamp);
            Assert.Equal(betas[i].Elapsed, result[i].Elapsed);
            Assert.Equal(betas[i].Counter, result[i].Counter);
            Assert.Equal(betas[i].IsValid, result[i].IsValid);
        }
    }

    [Fact]
    public async Task Write_and_read_dto_with_scaled_numbers_and_category()
    {
        var ct = TestContext.Current.CancellationToken;
        var gammas = Gamma.DataSet1();

        var writer = new Writer(gammas.Length)
            .AddCategory(gammas.Select(x => x.Sensor), "Sensor")
            .AddScaledNumber32(gammas.Select(x => x.Pressure), "Pressure", decimalPlaces: 3)
            .AddScaledNumber64(gammas.Select(x => x.Temperature), "Temperature", decimalPlaces: 4);

        var storage = new InMemoryStorage();
        await writer.WriteToAsync(storage, ct);

        var hydrator = new ReflectionBasedHydrator<Gamma>();
        var reader = new ReadBuilder();
        var result = await reader.ReadFromAsync(storage, hydrator, ct);

        Assert.Equal(gammas.Length, result.Length);
        for (var i = 0; i < gammas.Length; i++)
        {
            Assert.Equal(gammas[i].Sensor, result[i].Sensor);
            Assert.Equal(gammas[i].Pressure, result[i].Pressure, tolerance: 0.001f);
            Assert.Equal(gammas[i].Temperature, result[i].Temperature, tolerance: 0.0001);
        }
    }

    [Fact]
    public void Get_column_bindings_from_type_cache()
    {
        var bindings = TypeCache.Get<PowerPlant>();
        Assert.Equal(5, bindings.Count);
    }
}
