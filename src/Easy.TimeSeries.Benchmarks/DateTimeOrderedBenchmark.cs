namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class DateTimeOrderedBenchmark
{
    /// <summary>
    /// Precision drives how many bits each delta-of-delta needs, so it materially changes encode/decode cost.
    /// </summary>
    [Params(TimePrecision.Milliseconds, TimePrecision.Seconds)]
    public TimePrecision Precision { get; set; }

    [Params(16, 1024)]
    public int RowCount { get; set; }

    private DateTime[] data = [];
    private byte[] readBytes = [];

    [GlobalSetup]
    public void Setup()
    {
        data = GenerateAscending(RowCount);
        readBytes = CreateReadBytes(data, Precision);
    }

    /// <summary>
    /// Self-contained: rent, encode all rows, flush (writes the trailing partial word + ColumnHeader), return the
    /// buffer to the pool. No shared mutable state, so nothing accumulates across invocations.
    /// </summary>
    [Benchmark]
    public int Write()
    {
        using var buffer = CreateFramedBuffer(data.Length, Precision);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new DateTimeOrderedWriter(bitWriter, Precision);
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
        var reader = new DateTimeOrderedReader(readBytes, Precision);
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
        var writer = new DateTimeOrderedWriter(bitWriter, precision);
        foreach (var time in values)
        {
            writer.Write(time);
        }

        bitWriter.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Mirrors Writer.CreateWriters: reserve the ColumnHeader prefix so Flush can backfill it and the reader can
    /// parse it from the front of the buffer.
    /// </summary>
    private static PooledArrayBufferWriter CreateFramedBuffer(int valueCount, TimePrecision precision)
    {
        var sizeHint = DateTimeOrderedWriter.GetSizeHint(valueCount, precision);
        return Util.CreateFramedBuffer(sizeHint);
    }

    /// <summary>
    /// Deterministic ascending series (no clock, no RNG) with varied gaps to exercise every delta-of-delta branch.
    /// </summary>
    private static DateTime[] GenerateAscending(int count)
    {
        int[] stepsMs = [1000, 1000, 3000, 650, 794, 1, 840_000, 180_000, 180_000, 60_000, 120_000, 240_000];
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
