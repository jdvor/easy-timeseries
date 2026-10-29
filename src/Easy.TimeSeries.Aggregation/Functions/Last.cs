namespace Easy.TimeSeries.Aggregation.Functions;

/// <summary>
/// Value of the slice's last row, the one with the latest timestamp; among equal timestamps the row written last in
/// the raw file wins. A copy, not a computation: NaN is returned as is.
/// </summary>
public sealed class Last<TIn> : IAggregationFunction<TIn>
{
    public Last(string column, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(column), label ?? FunctionLabels.Of(column, "Last"), output)
    {
    }

    public Last(int columnIndex, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(columnIndex), label ?? FunctionLabels.Of(columnIndex, "Last"), output)
    {
    }

    private Last(AggregationInput input, string label, OutputEncoding? output)
    {
        Inputs = [input];
        Name = label;
        Output = output ?? OutputEncoding.SameAsInput;
    }

    public string Name { get; }

    public IReadOnlyList<AggregationInput> Inputs { get; }

    public OutputEncoding Output { get; }

    public TIn Compute(in AggregationSlice slice) => slice.Values<TIn>(0)[^1];
}
