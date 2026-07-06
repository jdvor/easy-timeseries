namespace Easy.TimeSeries.Tests;

using Easy.TimeSeries.Storage;

/// <summary>
/// Round-trips the Brotli-compressed raw numeric columns (<see cref="Writer.AddFloatRandom"/>,
/// <see cref="Writer.AddDoubleRandom"/>, <see cref="Writer.AddInt64Random"/>). Raw storage is
/// bit-preserving, so all assertions use exact equality.
/// </summary>
public class RawColumnTests
{
    [Fact]
    public async Task Float_and_double_raw_columns_round_trip_exactly()
    {
        var ct = TestContext.Current.CancellationToken;
        var alphas = Alpha.DataSet1();

        var writer = new Writer(alphas.Length)
            .AddInt32(alphas.Select(x => x.Age), "Age")
            .AddFloatRandom(alphas.Select(x => x.Radiation), "Radiation")
            .AddDoubleRandom(alphas.Select(x => x.TorqueRatio), "Torque");

        var storage = new InMemoryStorage();
        await writer.WriteToAsync(storage, ct);

        var result = await Reader.ReadFromAsync(storage, new ReflectionBasedMaterializer<Alpha>(), null, ct);

        Assert.Equal(alphas.Length, result.Length);
        for (var i = 0; i < alphas.Length; i++)
        {
            Assert.Equal(alphas[i].Age, result[i].Age);
            Assert.Equal(alphas[i].Radiation, result[i].Radiation);
            Assert.Equal(alphas[i].TorqueRatio, result[i].TorqueRatio);
        }
    }

    [Fact]
    public async Task Int64_raw_column_round_trips_exactly()
    {
        var ct = TestContext.Current.CancellationToken;
        var betas = Beta.DataSet1();

        var writer = new Writer(betas.Length)
            .AddTimeOrdered(betas.Select(x => x.Timestamp), "Timestamp")
            .AddInterval(betas.Select(x => x.Elapsed), "Elapsed")
            .AddInt64Random(betas.Select(x => x.Counter), "Counter")
            .AddBool(betas.Select(x => x.IsValid), "IsValid");

        var storage = new InMemoryStorage();
        await writer.WriteToAsync(storage, ct);

        var result = await Reader.ReadFromAsync(storage, new ReflectionBasedMaterializer<Beta>(), null, ct);

        Assert.Equal(betas.Length, result.Length);
        for (var i = 0; i < betas.Length; i++)
        {
            Assert.Equal(betas[i].Counter, result[i].Counter);
            Assert.Equal(betas[i].IsValid, result[i].IsValid);
        }
    }

    [Fact]
    public async Task Raw_columns_round_trip_large_uncorrelated_data()
    {
        var ct = TestContext.Current.CancellationToken;
        const int n = 2000;
        var ages = Numbers.RandIntSeq(n, 100).ToArray();
        var radiation = Numbers.RandFloatSeq(n, 50_000, -50_000).ToArray();
        var torque = Numbers.RandDoubleSeq(n, 1_000, -1_000).ToArray();

        var writer = new Writer(n)
            .AddInt32Random(ages, "Age")
            .AddFloatRandom(radiation, "Radiation")
            .AddDoubleRandom(torque, "Torque");

        var storage = new InMemoryStorage();
        await writer.WriteToAsync(storage, ct);

        var result = await Reader.ReadFromAsync(storage, new ReflectionBasedMaterializer<Alpha>(), null, ct);

        Assert.Equal(n, result.Length);
        for (var i = 0; i < n; i++)
        {
            Assert.Equal(ages[i], result[i].Age);
            Assert.Equal(radiation[i], result[i].Radiation);
            Assert.Equal(torque[i], result[i].TorqueRatio);
        }
    }
}
