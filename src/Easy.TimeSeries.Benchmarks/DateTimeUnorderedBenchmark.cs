namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Attributes;

/// <summary>
/// The escape hatch for timestamps that <see cref="DateTimeOrderedWriter"/> cannot take - unsorted, repeated, or
/// outside its epoch window. It stores a full-width value per row instead of a delta-of-delta, so this benchmark
/// is the counterweight to <see cref="DateTimeOrderedBenchmark"/>: it shows what ordering actually buys.
/// </summary>
[MemoryDiagnoser]
public class DateTimeUnorderedBenchmark
{
    /// <summary>Kept alongside the ordered benchmark's parameters so the two are directly comparable.</summary>
    [Params(TimePrecision.Milliseconds, TimePrecision.Seconds)]
    public TimePrecision Precision { get; set; }

    [Params(16, 1024)]
    public int RowCount { get; set; }

    private DateTime[] data = [];
    private byte[] readBytes = [];

    [GlobalSetup]
    public void Setup()
    {
        data = GenerateUnordered(RowCount);
        readBytes = CreateReadBytes(data, Precision);
    }

    [Benchmark]
    public int Write()
    {
        using var buffer = CreateFramedBuffer(data.Length, Precision);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new DateTimeUnorderedWriter(bitWriter, Precision);
        foreach (var time in data)
        {
            writer.Write(time);
        }

        bitWriter.Flush();
        return buffer.WrittenCount;
    }

    [Benchmark]
    public int Read()
    {
        var reader = new DateTimeUnorderedReader(readBytes, Precision);
        var acc = 0;
        for (var i = 0; i < data.Length; i++)
        {
            acc += reader.Read().Millisecond;
        }

        return acc;
    }

    private static byte[] CreateReadBytes(DateTime[] values, TimePrecision precision)
    {
        using var buffer = CreateFramedBuffer(values.Length, precision);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new DateTimeUnorderedWriter(bitWriter, precision);
        foreach (var time in values)
        {
            writer.Write(time);
        }

        bitWriter.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static PooledArrayBufferWriter CreateFramedBuffer(int valueCount, TimePrecision precision)
        => Util.CreateFramedBuffer(DateTimeUnorderedWriter.GetSizeHint(valueCount, precision));

    /// <summary>
    /// Deterministic out-of-order series (no clock, no RNG): forward and backward hops, repeats, and values on
    /// both sides of the format epoch - the cases that force a column onto this encoder in the first place.
    /// </summary>
    private static DateTime[] GenerateUnordered(int count)
    {
        int[] stepsMs = [1000, -500, 240_000, -180_000, 0, 60_000, -1, 840_000, -120_000, 3000];
        var result = new DateTime[count];
        var t = new DateTime(2023, 11, 23, 20, 51, 17, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
        {
            result[i] = t;
            t = t.AddMilliseconds(stepsMs[i % stepsMs.Length]);
        }

        return result;
    }
}
