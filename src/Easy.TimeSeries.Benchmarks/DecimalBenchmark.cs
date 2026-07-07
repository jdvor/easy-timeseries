namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Attributes;

/// <summary>
/// <see cref="DecimalWriter"/> is more than a shim over <see cref="Xor64Writer"/>: it interleaves two XOR streams
/// per record (the low 64 mantissa bits and a packed meta word) and calls <see cref="decimal.GetBits(decimal, Span{int})"/>.
/// The data uses a fixed scale so the meta word stays constant - the common case, where meta collapses to one bit
/// per record after the first.
/// </summary>
[MemoryDiagnoser]
public class DecimalBenchmark
{
    [Params(16, 1024)]
    public int RowCount { get; set; }

    private decimal[] data = [];
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
        var writer = new DecimalWriter(bitWriter);
        foreach (var value in data)
        {
            writer.Write(value);
        }

        bitWriter.Flush();
        return buffer.WrittenCount;
    }

    [Benchmark]
    public decimal Read()
    {
        var reader = new DecimalReader(readBytes);
        decimal acc = 0;
        for (var i = 0; i < data.Length; i++)
        {
            acc += reader.Read();
        }

        return acc;
    }

    private static byte[] CreateReadBytes(decimal[] values)
    {
        using var buffer = CreateFramedBuffer(values.Length);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new DecimalWriter(bitWriter);
        foreach (var value in values)
        {
            writer.Write(value);
        }

        bitWriter.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Two streams, each up to ~9.6 bytes per value in the worst case, so ~24 bytes per record is a safe ceiling
    /// that keeps the Write benchmark free of buffer-grow allocations.
    /// </summary>
    private static PooledArrayBufferWriter CreateFramedBuffer(int valueCount)
        => Util.CreateFramedBuffer((valueCount * 24) + 16);

    /// <summary>Deterministic monetary-like series with a constant scale (2 decimal places) and small cumulative deltas.</summary>
    private static decimal[] Generate(int count)
    {
        int[] centSteps = [1, 5, 2, 13, 0, 7, 1, 25];
        var result = new decimal[count];
        var cents = 100_00; // 100.00
        for (var i = 0; i < count; i++)
        {
            // Explicit scale 2 on every value keeps the packed meta word constant, so it costs one bit per record.
            result[i] = new decimal(cents, 0, 0, isNegative: false, scale: 2);
            cents += centSteps[i % centSteps.Length];
        }

        return result;
    }
}
