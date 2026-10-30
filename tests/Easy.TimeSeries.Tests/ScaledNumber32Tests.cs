namespace Easy.TimeSeries.Tests;

using Easy.TimeSeries.Storage;

public class ScaledNumber32Tests
{
    private readonly ITestOutputHelper output;

    public ScaledNumber32Tests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Theory]
    [InlineData(5)]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(2)]
    [InlineData(1)]
    public void WriteAndReadBackOnLinear(int precision)
    {
        var testData = Numbers.LinearFloatSeq(1000, 50d, 0d, precision).ToArray();
        var acceptableLength = testData.Length * 3 + 2;
        WriteAndReadBack("stairs", testData, precision, acceptableLength);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(2)]
    [InlineData(1)]
    public void WriteAndReadBackOnStairs(int precision)
    {
        var testData = Numbers.StairsFloatSeq(1000, 10, 50d, 0d, precision).ToArray();
        var acceptableLength = testData.Length * 3 + 2;
        WriteAndReadBack("linear", testData, precision, acceptableLength);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(2)]
    public void WriteAndReadBackOnRandom(int precision)
    {
        var testData = Numbers.RandFloatSeq(1000, 1_000_000d, -1_000_000d, precision).ToArray();
        var acceptableLength = testData.Length * 6 + 2;
        WriteAndReadBack("random", testData, precision, acceptableLength);
    }

    private void WriteAndReadBack(string useCase, float[] testData, int precision, int acceptableLength)
    {
        var scale = (int)Math.Pow(10, precision);
        var (bp, bw) = BufferUtil.CreateBitWriter(sizeHint: testData.Length * sizeof(ulong));

        var writer = new ScaledNumber32Writer(bw, scale);
        foreach (var value in testData)
        {
            writer.Write(value);
        }

        bw.Flush();
        var buffer = bp.WrittenSpan;
        output.WriteLine($"use case: {useCase}, precision: {precision}, written: {buffer.Length}B");

        Assert.True(
            buffer.Length <= acceptableLength,
            $"buffer.Length ({buffer.Length}) <= acceptableLength ({acceptableLength}), precision: {precision}");

        var reader = new ScaledNumber32Reader(buffer, scale);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            var diff = Math.Abs(expected - actual);
            var ok = (int)(diff * scale) == 0;
            Assert.True(ok, $"difference {diff}, expected: {expected}, actual: {actual}, precision: {precision}");
        }
    }
    [Fact]
    public async Task Double_overload_keeps_digits_beyond_float_precision()
    {
        var ct = TestContext.Current.CancellationToken;

        // 123456789 exceeds 2^24, so 1234567.89 narrowed to float first loses its last digit.
        Assert.Equal(123_456_789, await WriteAndReadScaledIntegerAsync(w => w.AddScaledNumber32([1234567.89d], "x", 2), ct));
        Assert.Equal(123_456_788, await WriteAndReadScaledIntegerAsync(w => w.AddScaledNumber32([1234567.89f], "x", 2), ct));
    }

    [Fact]
    public async Task Float_and_double_overloads_encode_float_input_identically()
    {
        var ct = TestContext.Current.CancellationToken;
        float[] values = [0f, 1.5f, -2.25f, 3.13f, 12345.67f, 0.01f];

        var fromFloat = await WriteAsync(w => w.AddScaledNumber32(values, "x", 2), values.Length, ct);
        var fromDouble = await WriteAsync(w => w.AddScaledNumber32(values.Select(v => (double)v), "x", 2), values.Length, ct);

        Assert.Equal(fromFloat, fromDouble);
    }

    [Fact]
    public async Task Integer_collection_expression_binds_to_the_float_overload()
    {
        var ct = TestContext.Current.CancellationToken;

        // Compiles only if the call is not ambiguous; float is the better conversion target for int literals.
        var bytes = await WriteAsync(w => w.AddScaledNumber32([1, 2, 3], "x", 2), 3, ct);
        Assert.NotEmpty(bytes);
    }

    private static async Task<int> WriteAndReadScaledIntegerAsync(Func<Writer, Writer> add, CancellationToken ct)
    {
        var bytes = await WriteAsync(add, 1, ct);
        var (_, columns) = Header.ReadLayout(bytes);
        var reader = new Int32Reader(bytes.AsSpan(columns[0].Start, columns[0].Length));
        return reader.Read();
    }

    private static async Task<byte[]> WriteAsync(Func<Writer, Writer> add, int rows, CancellationToken ct)
    {
        using var storage = new InMemoryStorage();
        using var writer = add(new Writer(rows));
        await writer.WriteToAsync(storage, ct);
        return storage.WrittenSpan.ToArray();
    }
}
