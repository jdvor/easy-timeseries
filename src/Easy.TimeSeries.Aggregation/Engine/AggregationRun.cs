namespace Easy.TimeSeries.Aggregation.Engine;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Paths;
using System.Buffers;

/// <summary>
/// State of one <see cref="Downsampler"/> run: reads raw files chunk by chunk, orders each chunk into aggregation
/// slices, computes every function per slice, and finally writes the aggregated file.
/// </summary>
/// <remarks>
/// Chunks are disjoint and processed in time order, so output rows stay sorted by (time, group) without a global sort.
/// The only state carried across chunks is the output buffers and the group map.
/// </remarks>
internal sealed class AggregationRun : IDisposable
{
    private readonly AggregationSpec spec;
    private readonly PathBuilder rawPaths;
    private readonly DateTime spanStart;
    private readonly long sliceTicks;
    private readonly SpecBinding binding;
    private readonly GroupMap? groups;
    private readonly GrowableBuffer<DateTime> outTimes = new();
    private readonly GrowableBuffer<int> outGroups = new();
    private readonly GrowableBuffer<int> outCounts = new();
    private ChunkBuffers? chunk;
    private ChunkSink? sink;
    private ResultBuffer[] results = [];
    private int rawFiles;
    private long rawRows;

    public AggregationRun(AggregationSpec spec, PathBuilder rawPaths, DateTime spanStart)
    {
        this.spec = spec;
        this.rawPaths = rawPaths;
        this.spanStart = spanStart;
        sliceTicks = spec.AggregationTimeSpan.ToTimeSpan().Ticks;
        binding = new SpecBinding(spec);
        groups = spec.GroupColumn is { } group ? new GroupMap(group) : null;
    }

    public async Task<AggregationResult> ExecuteAsync(
        IReadOnlyList<RawFile> files,
        IWriteStorage target,
        CancellationToken cancellationToken)
    {
        var ordered = files.Select((f, i) => (File: f, Order: i)).OrderBy(x => x.File.BucketStart).ThenBy(x => x.Order).ToArray();
        var i = 0;
        while (i < ordered.Length)
        {
            var chunkStart = ChunkStart(ordered[i].File.BucketStart);
            for (; i < ordered.Length && ChunkStart(ordered[i].File.BucketStart) == chunkStart; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await LoadAsync(ordered[i].File, cancellationToken).ConfigureAwait(false);
            }

            ProcessChunk(chunkStart);
        }

        if (outTimes.Count == 0)
        {
            return new AggregationResult(0, groups?.Count ?? 0, rawFiles, rawRows);
        }

        await WriteAsync(target, cancellationToken).ConfigureAwait(false);
        return new AggregationResult(outTimes.Count, groups?.Count ?? 0, rawFiles, rawRows);
    }

    public void Dispose()
    {
        chunk?.Dispose();
        outTimes.Dispose();
        outGroups.Dispose();
        outCounts.Dispose();
        foreach (var result in results)
        {
            result.Dispose();
        }
    }

    /// <summary>
    /// Every aggregation slice lies inside one chunk: a raw file, or the raw files of one slice when the aggregation
    /// time span is longer than the raw storage time span.
    /// </summary>
    private DateTime ChunkStart(DateTime bucketStart)
        => spec.AggregationTimeSpan.ToTimeSpan() > rawPaths.Granularity.MinLength()
            ? spec.AggregationTimeSpan.GetSliceStart(bucketStart)
            : bucketStart;

    private async Task LoadAsync(RawFile file, CancellationToken cancellationToken)
    {
        var name = file.Name ?? file.Storage.ToString();
        var bytes = await file.Storage.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!Header.TryReadFrom(bytes.Span, out var header, out _))
        {
            throw new AggregationException("The raw file has no valid header.", name);
        }

        var fileBinding = binding.Bind(header, name);
        if (chunk is null)
        {
            chunk = new ChunkBuffers([.. binding.Slots.Select(x => x.Type)]);
            sink = new ChunkSink(chunk, groups);
            results = [.. binding.Functions.Select(x => ResultBuffer.Create(x.Function))];
        }

        sink!.BeginFile(fileBinding, header.Columns.Length, chunk.Count, name);
        Reader.ReadFrom(bytes.Span, sink, new ReadOptions(ColumnIndexes: fileBinding.ReadColumns));

        var rows = Math.Max(sink.FileRows, 0);
        var bucketEnd = rawPaths.GetBucketEnd(file.BucketStart);
        var times = chunk.Data.Times;
        for (var row = chunk.Count; row < chunk.Count + rows; row++)
        {
            if (times[row] < file.BucketStart || times[row] >= bucketEnd)
            {
                throw new AggregationException(
                    $"Row {row - chunk.Count} at {times[row]:o} lies outside the raw file's time span "
                    + $"[{file.BucketStart:o}, {bucketEnd:o}).",
                    name,
                    column: binding.TimeLabel);
            }
        }

        chunk.Count += rows;
        rawRows += rows;
        ++rawFiles;
    }

    private void ProcessChunk(DateTime chunkStart)
    {
        if (chunk is not { Count: > 0 })
        {
            return;
        }

        var count = chunk.Count;
        var order = new RowOrder(chunk, groups is null ? null : RankGroups(count), chunkStart.Ticks, sliceTicks);
        if (!order.IsSorted(count))
        {
            var permutation = ArrayPool<int>.Shared.Rent(count);
            try
            {
                var span = permutation.AsSpan(0, count);
                for (var i = 0; i < count; i++)
                {
                    span[i] = i;
                }

                span.Sort(order);
                chunk.Permute(span);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(permutation);
            }
        }

        ComputeSlices(chunkStart, count);
        chunk.Count = 0;
    }

    /// <summary>Ordinal rank of each group id present in the chunk, so the row sort compares integers.</summary>
    private int[] RankGroups(int count)
    {
        var ids = chunk!.GroupIds.AsSpan(0, count).ToArray().Distinct().ToArray();
        Array.Sort(ids, (a, b) => string.CompareOrdinal(groups![a], groups[b]));
        var ranks = new int[groups!.Count];
        for (var rank = 0; rank < ids.Length; rank++)
        {
            ranks[ids[rank]] = rank;
        }

        return ranks;
    }

    private void ComputeSlices(DateTime chunkStart, int count)
    {
        var data = chunk!.Data;
        var times = data.Times;
        var groupIds = chunk.GroupIds;
        var functions = binding.Functions;
        var start = 0;
        while (start < count)
        {
            var slice = (times[start].Ticks - chunkStart.Ticks) / sliceTicks;
            var group = groups is null ? -1 : groupIds[start];
            var end = start + 1;
            while (end < count
                   && (times[end].Ticks - chunkStart.Ticks) / sliceTicks == slice
                   && (groups is null || groupIds[end] == group))
            {
                ++end;
            }

            var sliceStart = new DateTime(chunkStart.Ticks + (slice * sliceTicks), DateTimeKind.Utc);
            var sliceEnd = new DateTime(sliceStart.Ticks + sliceTicks, DateTimeKind.Utc);
            var groupValue = group < 0 ? null : groups![group];
            data.NextSlice();
            for (var f = 0; f < functions.Count; f++)
            {
                var view = new AggregationSlice(data, functions[f].InputSlots, start, end - start, sliceStart, sliceEnd, groupValue);
                results[f].Compute(view);
            }

            outTimes.Add(sliceStart);
            outGroups.Add(group);
            outCounts.Add(end - start);
            start = end;
        }
    }

    private async Task WriteAsync(IWriteStorage target, CancellationToken cancellationToken)
    {
        var encodings = binding.ResolveOutputs();
        var functions = binding.Functions;
        var rows = outTimes.Count;
        using var writer = new Writer(rows);
        AddColumn(binding.TimeLabel, () => writer.AddTimeOrdered(outTimes.Segment, binding.TimeLabel, binding.TimePrecision));
        if (groups is not null)
        {
            AddColumn(spec.GroupColumn!, () => writer.AddCategory(GroupValues(rows), spec.GroupColumn!));
        }

        AddColumn(spec.CountColumn, () => writer.AddInt32(outCounts.Segment, spec.CountColumn));
        for (var f = 0; f < functions.Count; f++)
        {
            var (result, label, encoding) = (results[f], functions[f].Function.Name, encodings[f]);
            AddColumn(label, () => result.WriteTo(writer, label, encoding));
        }

        await writer.WriteToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reports a column the core writer rejects, e.g. a Category column over its size limit, by label.</summary>
    private static void AddColumn(string label, Action add)
    {
        try
        {
            add();
        }
        catch (ArgumentException ex)
        {
            throw new AggregationException($"Aggregated column '{label}' cannot be written: {ex.Message}", column: label, innerException: ex);
        }
    }

    private IEnumerable<string> GroupValues(int rows)
    {
        for (var i = 0; i < rows; i++)
        {
            yield return groups![outGroups.Items[i]];
        }
    }

    /// <summary>Row order within a chunk: aggregation slice, group rank, timestamp, original row index.</summary>
    private readonly struct RowOrder(ChunkBuffers chunk, int[]? ranks, long chunkStartTicks, long sliceTicks) : IComparer<int>
    {
        public int Compare(int a, int b)
        {
            var times = chunk.Data.Times;
            var ta = times[a].Ticks;
            var tb = times[b].Ticks;
            var c = ((ta - chunkStartTicks) / sliceTicks).CompareTo((tb - chunkStartTicks) / sliceTicks);
            if (c != 0)
            {
                return c;
            }

            if (ranks is not null)
            {
                c = ranks[chunk.GroupIds[a]].CompareTo(ranks[chunk.GroupIds[b]]);
                if (c != 0)
                {
                    return c;
                }
            }

            c = ta.CompareTo(tb);

            // Array.Sort is not stable; the original index keeps equal timestamps in raw file order.
            return c != 0 ? c : a.CompareTo(b);
        }

        /// <summary>Whether the rows are already in order, the common case for one ungrouped ordered raw file.</summary>
        public bool IsSorted(int count)
        {
            for (var i = 1; i < count; i++)
            {
                if (Compare(i - 1, i) > 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
