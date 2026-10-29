namespace Easy.TimeSeries.Aggregation;

using System.Numerics;
using System.Runtime.CompilerServices;

/// <summary>
/// The raw rows of one aggregation time span and one group, as seen by an aggregation function. Rows are in time
/// order, input values are aligned row by row, and a slice is never empty.
/// </summary>
/// <remarks>
/// A slice is a view over the engine's pooled column buffers; it allocates nothing. Being a <c>ref struct</c>, it
/// cannot outlive the <c>Compute</c> call it is passed to.
/// </remarks>
public readonly ref struct AggregationSlice
{
    private readonly SliceData data;
    private readonly int[] slots;
    private readonly int projected;
    private readonly int offset;

    internal AggregationSlice(
        SliceData data,
        int[] slots,
        int offset,
        int count,
        DateTime start,
        DateTime end,
        string? group)
        : this(data, slots, -1, offset, count, start, end, group)
    {
    }

    private AggregationSlice(
        SliceData data,
        int[] slots,
        int projected,
        int offset,
        int count,
        DateTime start,
        DateTime end,
        string? group)
    {
        this.data = data;
        this.slots = slots;
        this.projected = projected;
        this.offset = offset;
        Count = count;
        Start = start;
        End = end;
        Group = group;
    }

    /// <summary>Raw rows in the slice, never zero.</summary>
    public int Count { get; }

    /// <summary>Start of the slice's aggregation time span, inclusive.</summary>
    public DateTime Start { get; }

    /// <summary>End of the slice's aggregation time span, exclusive.</summary>
    public DateTime End { get; }

    /// <summary>Group value, or <c>null</c> without grouping.</summary>
    public string? Group { get; }

    /// <summary>Timestamps of the rows, non-decreasing.</summary>
    public ReadOnlySpan<DateTime> Times => Data.Times.AsSpan(offset, Count);

    private SliceData Data => data ?? throw new InvalidOperationException("The slice is not initialized.");

    /// <summary>Values of one input within the slice, in row order.</summary>
    /// <param name="input">Index into the function's <see cref="IAggregationFunction.Inputs"/>.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not the declared input type.</exception>
    public ReadOnlySpan<T> Values<T>(int input)
    {
        var slot = Slot(input);
        var d = Data;
        if (d.Types[slot] != typeof(T))
        {
            throw new InvalidOperationException(
                $"Input {input} decodes to {d.Types[slot].Name}, not {typeof(T).Name}.");
        }

        return new ReadOnlySpan<T>(Unsafe.As<T[]>(d.Columns[slot]), offset, Count);
    }

    /// <summary>
    /// Sorted copy of one input's values without NaN, shared by all functions on the same raw column within this
    /// slice. It is shorter than <see cref="Count"/> when NaN values were excluded, and empty when all were NaN.
    /// </summary>
    public ReadOnlySpan<T> Sorted<T>(int input)
        where T : unmanaged, INumber<T>
    {
        // Validates the type before using the shared cache.
        _ = Values<T>(input);
        return Data.Sorted<T>(Slot(input), offset, Count);
    }

    /// <summary>A view whose only input, at index 0, is <paramref name="input"/> of this slice.</summary>
    public AggregationSlice Project(int input)
    {
        var index = projected >= 0 ? CheckProjected(input) : CheckInput(input);
        return new AggregationSlice(Data, slots, index, offset, Count, Start, End, Group);
    }

    private int Slot(int input)
        => slots[projected >= 0 ? CheckProjected(input) : CheckInput(input)];

    private int CheckInput(int input)
    {
        var length = slots?.Length ?? 0;
        return (uint)input < (uint)length
            ? input
            : throw new ArgumentOutOfRangeException(nameof(input), input, $"The function has {length} inputs.");
    }

    private int CheckProjected(int input)
        => input == 0
            ? projected
            : throw new ArgumentOutOfRangeException(nameof(input), input, "A projected slice has one input.");
}
