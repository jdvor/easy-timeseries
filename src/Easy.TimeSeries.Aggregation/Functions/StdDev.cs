namespace Easy.TimeSeries.Aggregation.Functions;

using System.Numerics;

/// <summary>
/// Population standard deviation (divides by n) of one numeric column, skipping NaN, computed with Welford's method.
/// A single value yields 0; any infinite value yields positive infinity.
/// </summary>
/// <remarks>
/// Decimal inputs are converted to <see cref="double"/>: decimal has no square root, its squared differences overflow
/// for wide spreads, and a standard deviation is irrational in general. Int64 inputs are shifted by the first value
/// before the conversion, exactly in <see cref="Int128"/>, so values above 2^53 (e.g. nanosecond timestamps) keep
/// their spread; a shift does not change the standard deviation.
/// </remarks>
public sealed class StdDev<TIn> : IAggregationFunction<double>
    where TIn : unmanaged, INumber<TIn>
{
    public StdDev(string column, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(column), label ?? FunctionLabels.Of(column, "StdDev"), output)
    {
    }

    public StdDev(int columnIndex, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(columnIndex), label ?? FunctionLabels.Of(columnIndex, "StdDev"), output)
    {
    }

    private StdDev(AggregationInput input, string label, OutputEncoding? output)
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
        var values = slice.Values<TIn>(0);
        var origin = typeof(TIn) == typeof(long) && !values.IsEmpty ? long.CreateTruncating(values[0]) : 0L;
        var n = 0L;
        double mean = 0, m2 = 0;
        foreach (var v in values)
        {
            var x = typeof(TIn) == typeof(long)
                ? (double)((Int128)long.CreateTruncating(v) - origin)
                : double.CreateChecked(v);
            if (double.IsNaN(x))
            {
                continue;
            }

            if (double.IsInfinity(x))
            {
                return double.PositiveInfinity;
            }

            ++n;
            var delta = x - mean;
            mean += delta / n;
            m2 += delta * (x - mean);
        }

        return n == 0 ? double.NaN : Math.Sqrt(m2 / n);
    }
}
