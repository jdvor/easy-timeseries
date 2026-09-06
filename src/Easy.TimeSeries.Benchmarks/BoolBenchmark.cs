namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Attributes;

/// <summary>
/// <see cref="BoolWriter"/> is one of the few encoders that does not delegate to the 32/64-bit XOR + block family,
/// so it needs measuring on its own. It packs one bit per value with no branch on the data, which makes cost a
/// function of row count alone - the shape parameters used by the numeric benchmarks would be meaningless here.
/// </summary>
[MemoryDiagnoser]
public class BoolBenchmark
{
    [Params(16, 1024)]
    public int RowCount { get; set; }

    private bool[] data = [];
    private byte[] readBytes = [];

    [GlobalSetup]
    public void Setup()
    {
        data = Generate(RowCount);
        readBytes = CreateReadBytes(data);
    }

    [Benchmark]
    public int Write()
    {
        using var buffer = CreateFramedBuffer(data.Length);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new BoolWriter(bitWriter);
        foreach (var value in data)
        {
            writer.Write(value);
        }

        bitWriter.Flush();
        return buffer.WrittenCount;
    }

    [Benchmark]
    public int Read()
    {
        var reader = new BoolReader(readBytes);
        var acc = 0;
        for (var i = 0; i < data.Length; i++)
        {
            if (reader.Read())
            {
                acc++;
            }
        }

        return acc;
    }

    private static byte[] CreateReadBytes(bool[] values)
    {
        using var buffer = CreateFramedBuffer(values.Length);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new BoolWriter(bitWriter);
        foreach (var value in values)
        {
            writer.Write(value);
        }

        bitWriter.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static PooledArrayBufferWriter CreateFramedBuffer(int valueCount)
        => Util.CreateFramedBuffer(BoolWriter.GetSizeHint(valueCount));

    /// <summary>Deterministic alternating-with-runs pattern (no clock, no RNG); bit packing is data-independent.</summary>
    private static bool[] Generate(int count)
    {
        bool[] pattern = [true, false, true, true, false, false, false, true];
        var result = new bool[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = pattern[i % pattern.Length];
        }

        return result;
    }
}
