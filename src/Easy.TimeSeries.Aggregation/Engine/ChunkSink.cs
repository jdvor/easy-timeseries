namespace Easy.TimeSeries.Aggregation.Engine;

using Easy.TimeSeries.Abstractions;
using System.Runtime.CompilerServices;

/// <summary>
/// Receives the decoded columns of one raw file from the core reader and appends them to the chunk buffers. One raw
/// column can feed several targets, e.g. the time column and a Min over it.
/// </summary>
internal sealed class ChunkSink(ChunkBuffers buffers, GroupMap? groups) : IMaterializer<ChunkSink.Row>
{
    private const int TimeTarget = -1;
    private const int GroupTarget = -2;

    private int[][] targets = [];
    private int[] current = [];
    private int baseRow;
    private int position;
    private string? rawName;

    /// <summary>Rows of the file being read; -1 before its first column.</summary>
    public int FileRows { get; private set; } = -1;

    /// <summary>Prepares the targets of one raw file whose rows are appended after <paramref name="rowsSoFar"/>.</summary>
    public void BeginFile(FileBinding binding, int columnCount, int rowsSoFar, string? name)
    {
        var lists = new List<int>[columnCount];
        Add(binding.TimeColumn, TimeTarget);
        if (binding.GroupColumn >= 0)
        {
            Add(binding.GroupColumn, GroupTarget);
        }

        for (var slot = 0; slot < binding.SlotColumns.Length; slot++)
        {
            Add(binding.SlotColumns[slot], slot);
        }

        targets = [.. lists.Select(x => x?.ToArray() ?? [])];
        baseRow = rowsSoFar;
        rawName = name;
        FileRows = -1;

        void Add(int column, int target) => (lists[column] ??= []).Add(target);
    }

    public bool BeginColumn(int column, int rowCount)
    {
        if (FileRows < 0)
        {
            FileRows = rowCount;
            buffers.EnsureCapacity(baseRow + rowCount);
        }
        else if (rowCount != FileRows)
        {
            throw new AggregationException(
                $"Column {column} has {rowCount} rows, other columns have {FileRows}.",
                rawName);
        }

        current = targets[column];
        position = baseRow;
        return true;
    }

    public void Hydrate<TPropValue>(TPropValue value)
        where TPropValue : struct
    {
        var row = position++;
        foreach (var target in current)
        {
            var array = target == TimeTarget ? buffers.Data.Times : buffers.Data.Columns[target];

            // Binding guarantees the slot type equals the decoded type, and the time column decodes to DateTime.
            Unsafe.As<TPropValue[]>(array)[row] = value;
        }
    }

    public void Hydrate(string value)
    {
        var row = position++;
        foreach (var target in current)
        {
            if (target == GroupTarget)
            {
                buffers.GroupIds[row] = groups!.GetId(value);
            }
            else
            {
                Unsafe.As<string[]>(buffers.Data.Columns[target])[row] = value;
            }
        }
    }

    public Row[] GetResult() => [];

    /// <summary>Sentinel row type; the sink collects columns, not rows.</summary>
    internal sealed class Row;
}

/// <summary>Pooled column buffers of one chunk, reused across chunks of a run.</summary>
internal sealed class ChunkBuffers : IDisposable
{
    private int capacity;

    public ChunkBuffers(Type[] slotTypes)
    {
        Data = new SliceData(slotTypes);
    }

    public SliceData Data { get; }

    public int[] GroupIds { get; private set; } = [];

    /// <summary>Rows currently held.</summary>
    public int Count { get; set; }

    public void EnsureCapacity(int rows)
    {
        if (rows <= capacity)
        {
            return;
        }

        var length = Math.Max(rows, Math.Max(1024, capacity * 2));
        Data.Times = capacity == 0 ? (DateTime[])PooledArrays.Rent(typeof(DateTime), length) : (DateTime[])PooledArrays.Grow(Data.Times, typeof(DateTime), length, Count);
        GroupIds = capacity == 0 ? (int[])PooledArrays.Rent(typeof(int), length) : (int[])PooledArrays.Grow(GroupIds, typeof(int), length, Count);
        for (var slot = 0; slot < Data.Types.Length; slot++)
        {
            Data.Columns[slot] = capacity == 0
                ? PooledArrays.Rent(Data.Types[slot], length)
                : PooledArrays.Grow(Data.Columns[slot], Data.Types[slot], length, Count);
        }

        capacity = Data.Times.Length;
    }

    /// <summary>Reorders every buffer so that row <c>i</c> holds the former row <c>order[i]</c>.</summary>
    public void Permute(ReadOnlySpan<int> order)
    {
        Data.Times = (DateTime[])PooledArrays.Permute(Data.Times, typeof(DateTime), order);
        GroupIds = (int[])PooledArrays.Permute(GroupIds, typeof(int), order);
        for (var slot = 0; slot < Data.Types.Length; slot++)
        {
            Data.Columns[slot] = PooledArrays.Permute(Data.Columns[slot], Data.Types[slot], order);
        }
    }

    public void Dispose()
    {
        if (capacity > 0)
        {
            PooledArrays.Return(Data.Times);
            PooledArrays.Return(GroupIds);
            foreach (var column in Data.Columns)
            {
                PooledArrays.Return(column);
            }
        }

        capacity = 0;
        Data.Dispose();
    }
}
