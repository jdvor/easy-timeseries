namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Attributes;

/// <summary>
/// Represents the 32-bit XOR + block family. <see cref="FloatWriter"/>, <see cref="ScaledNumber32Writer"/> and
/// <see cref="TimeSpanWriter"/> all delegate to <see cref="Int32Writer"/>, adding only a cast or scale, so
/// benchmarking those separately would re-measure this same state machine.
/// </summary>
[MemoryDiagnoser]
public class Int32Benchmark
{
    /// <summary>Selects which branch of the XOR + block state machine the data drives.</summary>
    [Params(ValueShape.Constant, ValueShape.Drift, ValueShape.Jumpy)]
    public ValueShape Shape { get; set; }

    [Params(16, 1024)]
    public int RowCount { get; set; }

    private int[] data = [];
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
        var writer = new Int32Writer(bitWriter);
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
        var reader = new Int32Reader(readBytes);
        long acc = 0;
        for (var i = 0; i < data.Length; i++)
        {
            acc += reader.Read();
        }

        return acc;
    }

    private static byte[] CreateReadBytes(int[] values)
    {
        using var buffer = CreateFramedBuffer(values.Length);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new Int32Writer(bitWriter);
        foreach (var value in values)
        {
            writer.Write(value);
        }

        bitWriter.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Sized well above the worst case (~5.5 bytes per value for the fresh-block path) so the buffer never grows
    /// mid-run; that keeps the Write benchmark's allocation count at the single rented array we care about.
    /// </summary>
    private static PooledArrayBufferWriter CreateFramedBuffer(int valueCount)
        => Util.CreateFramedBuffer((valueCount * 8) + 16);

    /// <summary>Deterministic sequences (no clock, no RNG) that each target one encoder branch - see <see cref="ValueShape"/>.</summary>
    private static int[] Generate(int count, ValueShape shape)
    {
        var result = new int[count];
        switch (shape)
        {
            case ValueShape.Constant:
                Array.Fill(result, 42);
                break;

            case ValueShape.Drift:
                int[] steps = [1, 2, 1, 3, 0, 2, 1, 4];
                var drift = 1000;
                for (var i = 0; i < count; i++)
                {
                    result[i] = drift;
                    drift += steps[i % steps.Length];
                }

                break;

            case ValueShape.Jumpy:
                int[] jumps = [0, int.MaxValue, 1, -1, 123_456, 7, 999_999_999, -42];
                for (var i = 0; i < count; i++)
                {
                    result[i] = jumps[i % jumps.Length];
                }

                break;
        }

        return result;
    }
}
