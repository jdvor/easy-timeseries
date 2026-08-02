namespace Easy.TimeSeries.Parquet.Tests;

using Easy.TimeSeries;
using Easy.TimeSeries.Storage;
using System.IO;
using global::Parquet;
using global::Parquet.Serialization;

/// <summary>
/// End-to-end tests that build a time-series buffer with <see cref="Writer"/>, convert it to Parquet with
/// <see cref="TsToParquetConverter"/>, and read it back with Parquet.Net to verify the mapping and values.
/// </summary>
public class ConversionTests
{
    private const int Rows = 5;

    [Fact]
    public async Task Converts_all_supported_column_types_and_round_trips_values()
    {
        var ct = TestContext.Current.CancellationToken;
        var expected = SampleRows();

        await using var parquet = new MemoryStream();
        await ConvertSampleAsync(parquet, options: null, ct);

        parquet.Position = 0;
        var actual = (await ParquetSerializer.DeserializeAsync<PowerReading>(parquet, cancellationToken: ct)).Data;

        Assert.Equal(Rows, actual.Count);
        for (var i = 0; i < Rows; i++)
        {
            var e = expected[i];
            var a = actual[i];

            // DateTime is stored as a native Parquet TIMESTAMP; compare instants regardless of DateTimeKind.
            Assert.Equal(e.Ts.Ticks, a.Ts.Ticks);
            Assert.Equal(e.TsUnordered.Ticks, a.TsUnordered.Ticks);
            Assert.Equal(e.Duration, a.Duration);
            Assert.Equal(e.Temp, a.Temp);
            Assert.Equal(e.Value, a.Value);
            Assert.Equal(e.Price, a.Price);
            Assert.Equal(e.Count, a.Count);
            Assert.Equal(e.Big, a.Big);
            Assert.Equal(e.Flag, a.Flag);
            Assert.Equal(e.Approx, a.Approx, 0.001f);
            Assert.Equal(e.Approx64, a.Approx64, 0.0001);
            Assert.Equal(e.Fuel, a.Fuel);
            Assert.Equal(e.Rnd, a.Rnd);
            Assert.Equal(e.RndI, a.RndI);
            Assert.Equal(e.RndD, a.RndD);
            Assert.Equal(e.RndL, a.RndL);
        }
    }

    [Fact]
    public async Task Maps_columns_to_native_parquet_types()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var parquet = new MemoryStream();
        await ConvertSampleAsync(parquet, options: null, ct);

        parquet.Position = 0;
        var schema = await ParquetReader.ReadSchemaAsync(parquet);
        var byName = schema.DataFields.ToDictionary(f => f.Name, f => f.ClrType);

        Assert.Equal(typeof(DateTime), byName["Ts"]);
        Assert.Equal(typeof(long), byName["Duration"]);
        Assert.Equal(typeof(float), byName["Temp"]);
        Assert.Equal(typeof(double), byName["Value"]);
        Assert.Equal(typeof(decimal), byName["Price"]);
        Assert.Equal(typeof(int), byName["Count"]);
        Assert.Equal(typeof(long), byName["Big"]);
        Assert.Equal(typeof(bool), byName["Flag"]);
        Assert.Equal(typeof(float), byName["Approx"]);
        Assert.Equal(typeof(string), byName["Fuel"]);
    }

    [Fact]
    public async Task Uses_generated_name_for_unlabeled_column()
    {
        var ct = TestContext.Current.CancellationToken;

        using var storage = new InMemoryStorage();
        using (var writer = new Writer(Rows))
        {
            writer.AddInt32([1, 2, 3, 4, 5], string.Empty);
            await writer.WriteToAsync(storage, ct);
        }

        await using var parquet = new MemoryStream();
        await TsToParquetConverter.ConvertAsync(storage, parquet, cancellationToken: ct);

        parquet.Position = 0;
        var schema = await ParquetReader.ReadSchemaAsync(parquet);
        Assert.Equal("col0", schema.DataFields[0].Name);
    }

    [Fact]
    public async Task Splits_rows_into_multiple_row_groups_when_configured()
    {
        var ct = TestContext.Current.CancellationToken;

        using var storage = new InMemoryStorage();
        using (var writer = new Writer(Rows))
        {
            writer.AddInt32([1, 2, 3, 4, 5], "Count");
            await writer.WriteToAsync(storage, ct);
        }

        await using var parquet = new MemoryStream();
        await TsToParquetConverter.ConvertAsync(storage, parquet, new ParquetConversionOptions { RowGroupSize = 2 }, ct);

        parquet.Position = 0;
        await using var reader = await ParquetReader.CreateAsync(parquet, cancellationToken: ct);
        Assert.Equal(3, reader.RowGroupCount); // 5 rows / 2 per group => 3 groups (2, 2, 1)
    }

    private static async Task ConvertSampleAsync(Stream destination, ParquetConversionOptions? options, CancellationToken ct)
    {
        var rows = SampleRows();
        using var storage = new InMemoryStorage();
        using (var writer = new Writer(Rows))
        {
            writer.AddTimeOrdered(rows.Select(r => r.Ts), "Ts");
            writer.AddTimeUnordered(rows.Select(r => r.TsUnordered), "TsUnordered");
            writer.AddInterval(rows.Select(r => TimeSpan.FromTicks(r.Duration)), "Duration");
            writer.AddFloat(rows.Select(r => r.Temp), "Temp");
            writer.AddDouble(rows.Select(r => r.Value), "Value");
            writer.AddDecimal(rows.Select(r => r.Price), "Price");
            writer.AddInt32(rows.Select(r => r.Count), "Count");
            writer.AddInt64(rows.Select(r => r.Big), "Big");
            writer.AddBool(rows.Select(r => r.Flag), "Flag");
            writer.AddScaledNumber32(rows.Select(r => r.Approx), "Approx", decimalPlaces: 2);
            writer.AddScaledNumber64(rows.Select(r => r.Approx64), "Approx64", decimalPlaces: 3);
            writer.AddCategory(rows.Select(r => r.Fuel), "Fuel");
            writer.AddFloatRandom(rows.Select(r => r.Rnd), "Rnd");
            writer.AddInt32Random(rows.Select(r => r.RndI), "RndI");
            writer.AddDoubleRandom(rows.Select(r => r.RndD), "RndD");
            writer.AddInt64Random(rows.Select(r => r.RndL), "RndL");
            await writer.WriteToAsync(storage, ct);
        }

        await TsToParquetConverter.ConvertAsync(storage, destination, options, ct);
    }

    private static PowerReading[] SampleRows()
    {
        // 2020-01-01 UTC at millisecond resolution (matches the default TimePrecision).
        var baseTs = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var fuels = new[] { "coal", "gas", "wind", "gas", "solar" };
        var rows = new PowerReading[Rows];
        for (var i = 0; i < Rows; i++)
        {
            rows[i] = new PowerReading
            {
                Ts = baseTs.AddMinutes(i),
                TsUnordered = baseTs.AddMinutes(Rows - i),
                Duration = TimeSpan.FromSeconds(i + 1).Ticks,
                Temp = 20.5f + i,
                Value = 100.25 + i,
                Price = 12.34m + i,
                Count = 1000 + i,
                Big = 5_000_000_000L + i,
                Flag = i % 2 == 0,
                Approx = 1.25f + (i * 0.5f),
                Approx64 = 2.125 + (i * 0.001),
                Fuel = fuels[i],
                Rnd = 0.1f * i,
                RndI = i * 7,
                RndD = i * 1.5,
                RndL = i * 9_000_000_000L,
            };
        }

        return rows;
    }

    private sealed class PowerReading
    {
        public DateTime Ts { get; set; }
        public DateTime TsUnordered { get; set; }
        public long Duration { get; set; }
        public float Temp { get; set; }
        public double Value { get; set; }
        public decimal Price { get; set; }
        public int Count { get; set; }
        public long Big { get; set; }
        public bool Flag { get; set; }
        public float Approx { get; set; }
        public double Approx64 { get; set; }
        public string Fuel { get; set; } = string.Empty;
        public float Rnd { get; set; }
        public int RndI { get; set; }
        public double RndD { get; set; }
        public long RndL { get; set; }
    }
}
