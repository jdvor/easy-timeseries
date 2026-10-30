namespace Easy.TimeSeries.Aggregation.Functions;

/// <summary>Share of <c>true</c> values in one Bool column, from 0 to 1.</summary>
public sealed class TrueRatio : IAggregationFunction<double>
{
    public TrueRatio(string column, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<bool>(column), label ?? FunctionLabels.Of(column, "TrueRatio"), output)
    {
    }

    public TrueRatio(int columnIndex, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<bool>(columnIndex), label ?? FunctionLabels.Of(columnIndex, "TrueRatio"), output)
    {
    }

    private TrueRatio(AggregationInput input, string label, OutputEncoding? output)
    {
        Inputs = [input];
        Name = label;
        Output = output ?? OutputEncoding.FromInputPrecision;
    }

    public string Name { get; }

    public IReadOnlyList<AggregationInput> Inputs { get; }

    public OutputEncoding Output { get; }

    public double Compute(in AggregationSlice slice)
    {
        var values = slice.Values<bool>(0);
        var trues = 0;
        foreach (var v in values)
        {
            if (v)
            {
                ++trues;
            }
        }

        return (double)trues / values.Length;
    }
}
