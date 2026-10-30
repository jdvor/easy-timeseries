namespace Easy.TimeSeries.Tests;

using Easy.TimeSeries.Storage;

/// <summary>
/// A file with exactly one row is valid for every column type. Aggregated files often have one row (e.g. an hourly
/// aggregation stored hourly), so every encoder, including the Brotli-backed raw variants, must round-trip it.
/// </summary>
public class SingleRowTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 25, 14, 5, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Every_column_type_round_trips_a_single_row()
    {
        var ct = TestContext.Current.CancellationToken;
        object[] expected =
        [
            Timestamp,
            new DateTime(1999, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            TimeSpan.FromMinutes(90),
            1.5f,
            12345.6789d,
            12345.6789m,
            -2.25f,
            12345.67d,
            -42,
            5_000_000_000L,
            true,
            "ok",
            3.125f,
            -0.0001d,
            2_147_483_647,
            -9_223_372_036_854_775_808L,
        ];

        using var writer = new Writer(1)
            .AddTimeOrdered([Timestamp], "ts")
            .AddTimeUnordered([(DateTime)expected[1]], "tsu")
            .AddInterval([(TimeSpan)expected[2]], "span")
            .AddFloat([(float)expected[3]], "f32")
            .AddDouble([(double)expected[4]], "f64")
            .AddDecimal([(decimal)expected[5]], "dec")
            .AddScaledNumber32([(float)expected[6]], "sn32", 2)
            .AddScaledNumber64([(double)expected[7]], "sn64", 2)
            .AddInt32([(int)expected[8]], "i32")
            .AddInt64([(long)expected[9]], "i64")
            .AddBool([(bool)expected[10]], "flag")
            .AddCategory([(string)expected[11]], "cat")
            .AddFloatRandom([(float)expected[12]], "f32r")
            .AddDoubleRandom([(double)expected[13]], "f64r")
            .AddInt32Random([(int)expected[14]], "i32r")
            .AddInt64Random([(long)expected[15]], "i64r");

        using var storage = new InMemoryStorage();
        await writer.WriteToAsync(storage, ct);

        var materializer = new CapturingMaterializer();
        await Reader.ReadFromAsync(storage, materializer, null, ct);

        Assert.Equal(expected.Length, materializer.Columns.Count);
        for (var column = 0; column < expected.Length; column++)
        {
            Assert.Equal([expected[column]], materializer.Columns[column]);
        }
    }

    [Fact]
    public void Zero_rows_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Writer(0));
    }
}
