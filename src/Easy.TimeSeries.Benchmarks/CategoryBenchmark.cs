namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Attributes;

/// <summary>
/// The category column is a genuinely distinct algorithm: a label-to-id dictionary plus a variable-width id
/// (5 bits for ids 0-15, 16 bits above that). This isolates the codec - <see cref="CategoryWriter"/> over
/// precomputed ids and <see cref="CategoryReader"/> over a framed buffer - not the <see cref="IdAccumulator"/>
/// dictionary build, which is a separate write-path concern. Note the Read side also reconstructs the
/// <see cref="CategoryMap"/> from the buffer on construction; that cost is inherent to reading a category column.
/// </summary>
[MemoryDiagnoser]
public class CategoryBenchmark
{
    /// <summary>Distinct label count. 8 stays in the 5-bit small-id path; 100 forces the 16-bit large-id path for most ids.</summary>
    [Params(8, 100)]
    public int Cardinality { get; set; }

    [Params(16, 1024)]
    public int RowCount { get; set; }

    private short[] ids = [];
    private CategoryMap map = CategoryMap.Empty;
    private byte[] readBytes = [];

    [GlobalSetup]
    public void Setup()
    {
        var labels = new string[RowCount];
        var distinct = BuildDistinctLabels(Cardinality);
        for (var i = 0; i < RowCount; i++)
        {
            labels[i] = distinct[i % Cardinality];
        }

        var accumulator = new IdAccumulator(RowCount);
        accumulator.AddRange(labels);
        var (keys, substituted) = accumulator.BuildIterator();
        map = new CategoryMap(keys);
        ids = substituted.ToArray();
        readBytes = CreateReadBytes(ids, map);
    }

    [Benchmark]
    public int Write()
    {
        using var buffer = CreateFramedBuffer(ids.Length);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new CategoryWriter(bitWriter);
        foreach (var id in ids)
        {
            writer.Write(id);
        }

        bitWriter.Flush();
        return buffer.WrittenCount;
    }

    [Benchmark]
    public int Read()
    {
        var reader = new CategoryReader(readBytes);
        var acc = 0;
        for (var i = 0; i < ids.Length; i++)
        {
            acc += reader.Read().Length;
        }

        return acc;
    }

    private static byte[] CreateReadBytes(short[] values, CategoryMap categoryMap)
    {
        using var buffer = CreateFramedBuffer(values.Length);
        var bitWriter = new BitWriter(buffer, buffer.GetColumnHeaderMemory);
        var writer = new CategoryWriter(bitWriter);
        foreach (var id in values)
        {
            writer.Write(id);
        }

        bitWriter.Flush();

        // Mirror Writer.AddCategory: the label map is appended after the packed ids; CategoryReader locates it
        // via the DataLength stored in the ColumnHeader at the front of the buffer.
        categoryMap.WriteTo(buffer);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Worst case is the 16-bit large-id path (2 bytes per record); size above that to avoid buffer growth.</summary>
    private static PooledArrayBufferWriter CreateFramedBuffer(int valueCount)
        => Util.CreateFramedBuffer((valueCount * 2) + 512);

    private static string[] BuildDistinctLabels(int cardinality)
    {
        var labels = new string[cardinality];
        for (var i = 0; i < cardinality; i++)
        {
            labels[i] = $"category-{i:D3}";
        }

        return labels;
    }
}
