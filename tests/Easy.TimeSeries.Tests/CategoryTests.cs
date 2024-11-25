namespace Easy.TimeSeries.Tests;

using Xunit.Abstractions;

public class CategoryTests
{
    private readonly ITestOutputHelper output;

    public CategoryTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void WriteAndReadBack()
    {
        const int n = 1000;
        var testData = CreateTestData(n);

        var idAcc = new IdAccumulator(n);
        idAcc.AddRange(testData);
        var (keys, ids) = idAcc.BuildIterator();
        var map = new CategoryMap(keys);

        var sizeHint = CategoryWriter.GetSizeHint(n) + map.SizeHint;
        var (bp, bw) = BufferUtil.CreateBitWriter(sizeHint);
        var writer = new CategoryWriter(bw);
        foreach (var id in ids)
        {
            writer.Write(id);
        }

        bw.Flush();
        var mapWritten = map.WriteTo(bp);
        output.WriteLine($"map written: {mapWritten}B");

        var buffer = bp.WrittenSpan;
        output.WriteLine($"total written: {buffer.Length}B");

        var reader = new CategoryReader(buffer);
        foreach (var expected in testData)
        {
            var actual = reader.Read();
            Assert.Equal(expected, actual);
        }
    }

    private static string[] CreateTestData(int n)
    {
        var rnd = new Random(Environment.TickCount);
        var result = new string[n];
        for (var i = 0; i < n; i++)
        {
            var idx = rnd.Next(0, Keys.Length);
            var pct25 = rnd.Next(1, 101) <= 25;
            if (idx > 3 && pct25) // first 4 values: "Solar", "Hydro", "Oil", "Coal" are 25% more likely than rest
            {
                idx %= 4;
            }

            result[i] = Keys[idx];
        }

        return result;
    }

    private static readonly string[] Keys =
    {
        "Solar", "Hydro", "Oil", "Coal", "Nuclear", "Biogas", "Tidal", "Thermal gradient", "Wind"
    };
}
