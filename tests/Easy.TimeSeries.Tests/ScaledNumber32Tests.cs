namespace Easy.TimeSeries.Tests;

using Xunit.Abstractions;

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
        var acceptableLength = testData.Length * 4 + 2;
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
}