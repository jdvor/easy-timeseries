namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Attributes;

/// <summary>
/// The lowest-level primitive under every column type: <see cref="BitWriter.Write(ulong, int)"/> +
/// <see cref="BitWriter.Flush"/> and <see cref="BitReader.Read(int)"/>. This measures the per-bit-op floor
/// independent of any encoder, so a regression here is visible before it hides inside a codec benchmark.
/// The bit widths are chosen to stress the word-boundary carry logic: 1 (sub-byte), 17 (a non-divisor of 64
/// that repeatedly straddles word boundaries) and 64 (full-word, which hits the special-case shift in Write).
/// </summary>
[MemoryDiagnoser]
public class BitBufferBenchmark
{
    [Params(1, 17, 64)]
    public int BitCount { get; set; }

    [Params(16, 1024)]
    public int RowCount { get; set; }

    private ulong[] data = [];
    private byte[] readBytes = [];

    [GlobalSetup]
    public void Setup()
    {
        data = Generate(RowCount, BitCount);
        readBytes = CreateReadBytes(data, BitCount);
    }

    [Benchmark]
    public int Write()
    {
        using var buffer = CreateFramedBuffer(data.Length, BitCount);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        foreach (var value in data)
        {
            bitWriter.Write(value, BitCount);
            bitWriter.CommitRecord();
        }

        bitWriter.Flush();
        return buffer.WrittenCount;
    }

    [Benchmark]
    public ulong Read()
    {
        var reader = new BitReader(readBytes);
        ulong acc = 0;
        for (var i = 0; i < data.Length; i++)
        {
            acc += reader.Read(BitCount);
        }

        return acc;
    }

    private static byte[] CreateReadBytes(ulong[] values, int bitCount)
    {
        using var buffer = CreateFramedBuffer(values.Length, bitCount);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        foreach (var value in values)
        {
            bitWriter.Write(value, bitCount);
            bitWriter.CommitRecord();
        }

        bitWriter.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static PooledArrayBufferWriter CreateFramedBuffer(int valueCount, int bitCount)
        => Util.CreateFramedBuffer((valueCount * bitCount / 8) + Constants.WordByteSize + 16);

    /// <summary>Deterministic values masked to <paramref name="bitCount"/> so they always fit the requested width.</summary>
    private static ulong[] Generate(int count, int bitCount)
    {
        var mask = bitCount == 64 ? ulong.MaxValue : (1UL << bitCount) - 1;
        ulong[] pattern = [0UL, 1UL, 0xA5A5_A5A5_A5A5_A5A5UL, ulong.MaxValue, 0x1234_5678_9ABC_DEF0UL, 42UL];
        var result = new ulong[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = pattern[i % pattern.Length] & mask;
        }

        return result;
    }
}
