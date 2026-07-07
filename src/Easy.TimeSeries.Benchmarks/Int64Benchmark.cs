namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Attributes;

/// <summary>
/// Represents the 64-bit XOR + block family. <see cref="Int64Writer"/> is the thinnest wrapper over
/// <see cref="Xor64Writer"/>; <see cref="DoubleWriter"/>, <see cref="ScaledNumber64Writer"/> and
/// <see cref="DateTimeUnorderedWriter"/> all route through the same core, so this stands in for all of them.
/// </summary>
[MemoryDiagnoser]
public class Int64Benchmark
{
    /// <summary>Selects which branch of the XOR + block state machine the data drives.</summary>
    [Params(ValueShape.Constant, ValueShape.Drift, ValueShape.Jumpy)]
    public ValueShape Shape { get; set; }

    [Params(16, 1024)]
    public int RowCount { get; set; }

    private long[] data = [];
    private byte[] readBytes = [];

    [GlobalSetup]
    public void Setup()
    {
        data = Generate(RowCount, Shape);
        readBytes = CreateReadBytes(data);
    }

    [Benchmark]
    public int Write()
    {
        using var buffer = CreateFramedBuffer(data.Length);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new Int64Writer(bitWriter);
        foreach (var value in data)
        {
            writer.Write(value);
        }

        bitWriter.Flush();
        return buffer.WrittenCount;
    }

    [Benchmark]
    public long Read()
    {
        var reader = new Int64Reader(readBytes);
        long acc = 0;
        for (var i = 0; i < data.Length; i++)
        {
            acc += reader.Read();
        }

        return acc;
    }

    private static byte[] CreateReadBytes(long[] values)
    {
        using var buffer = CreateFramedBuffer(values.Length);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new Int64Writer(bitWriter);
        foreach (var value in values)
        {
            writer.Write(value);
        }

        bitWriter.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Sized well above the worst case (~9.6 bytes per value for the fresh-block path) so the buffer never grows
    /// mid-run and the Write benchmark measures only the encoder, not a pool re-rent.
    /// </summary>
    private static PooledArrayBufferWriter CreateFramedBuffer(int valueCount)
        => Util.CreateFramedBuffer((valueCount * 12) + 16);

    /// <summary>Deterministic sequences (no clock, no RNG) that each target one encoder branch - see <see cref="ValueShape"/>.</summary>
    private static long[] Generate(int count, ValueShape shape)
    {
        var result = new long[count];
        switch (shape)
        {
            case ValueShape.Constant:
                Array.Fill(result, 42L);
                break;

            case ValueShape.Drift:
                long[] steps = [1, 2, 1, 3, 0, 2, 1, 4];
                var drift = 1_000_000L;
                for (var i = 0; i < count; i++)
                {
                    result[i] = drift;
                    drift += steps[i % steps.Length];
                }

                break;

            case ValueShape.Jumpy:
                long[] jumps = [0, long.MaxValue, 1, -1, 123_456_789_012, 7, 999_999_999_999, -42];
                for (var i = 0; i < count; i++)
                {
                    result[i] = jumps[i % jumps.Length];
                }

                break;
        }

        return result;
    }
}
