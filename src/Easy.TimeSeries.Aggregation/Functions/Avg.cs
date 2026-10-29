namespace Easy.TimeSeries.Aggregation.Functions;

using System.Numerics;
using System.Runtime.InteropServices;

/// <summary>
/// Arithmetic mean of one numeric column, skipping NaN. The accumulator depends on the input type so the sum loses
/// no precision: <see cref="Int128"/> for integers, compensated (Neumaier) summation for <see cref="double"/>.
/// </summary>
/// <remarks>Decimal columns use <see cref="AvgDecimal"/>, which stays exact.</remarks>
public sealed class Avg<TIn> : IAggregationFunction<double>
    where TIn : unmanaged, INumber<TIn>
{
    public Avg(string column, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(column), label ?? FunctionLabels.Of(column, "Avg"), output)
    {
    }

    public Avg(int columnIndex, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(columnIndex), label ?? FunctionLabels.Of(columnIndex, "Avg"), output)
    {
    }

    private Avg(AggregationInput input, string label, OutputEncoding? output)
    {
        FunctionLabels.RejectDecimal<TIn>("Avg", nameof(AvgDecimal));
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
        if (typeof(TIn) == typeof(int))
        {
            Int128 sum = 0;
            foreach (var v in MemoryMarshal.Cast<TIn, int>(values))
            {
                sum += v;
            }

            return (double)sum / values.Length;
        }

        if (typeof(TIn) == typeof(long))
        {
            Int128 sum = 0;
            foreach (var v in MemoryMarshal.Cast<TIn, long>(values))
            {
                sum += v;
            }

            return (double)sum / values.Length;
        }

        if (typeof(TIn) == typeof(double))
        {
            return CompensatedMean(MemoryMarshal.Cast<TIn, double>(values));
        }

        var total = 0d;
        var n = 0;
        foreach (var v in values)
        {
            if (TIn.IsNaN(v))
            {
                continue;
            }

            total += double.CreateChecked(v);
            ++n;
        }

        return n == 0 ? double.NaN : total / n;
    }

    /// <summary>Neumaier summation; falls back to the plain sum once it is no longer finite.</summary>
    private static double CompensatedMean(ReadOnlySpan<double> values)
    {
        double sum = 0, compensation = 0, plain = 0;
        var n = 0;
        foreach (var x in values)
        {
            if (double.IsNaN(x))
            {
                continue;
            }

            ++n;
            plain += x;
            var t = sum + x;
            compensation += Math.Abs(sum) >= Math.Abs(x) ? (sum - t) + x : (x - t) + sum;
            sum = t;
        }

        if (n == 0)
        {
            return double.NaN;
        }

        return double.IsFinite(plain) ? (sum + compensation) / n : plain / n;
    }
}

/// <summary>Arithmetic mean of a <see cref="decimal"/> column, computed exactly in decimal.</summary>
public sealed class AvgDecimal : IAggregationFunction<decimal>
{
    public AvgDecimal(string column, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<decimal>(column), label ?? FunctionLabels.Of(column, "Avg"), output)
    {
    }

    public AvgDecimal(int columnIndex, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<decimal>(columnIndex), label ?? FunctionLabels.Of(columnIndex, "Avg"), output)
    {
    }

    private AvgDecimal(AggregationInput input, string label, OutputEncoding? output)
    {
        Inputs = [input];
        Name = label;
        Output = output ?? OutputEncoding.FromInputPrecision;
    }

    public string Name { get; }

    public IReadOnlyList<AggregationInput> Inputs { get; }

    public OutputEncoding Output { get; }

    public decimal Compute(in AggregationSlice slice)
    {
        var values = slice.Values<decimal>(0);
        var sum = 0m;
        foreach (var v in values)
        {
            sum += v;
        }

        return sum / values.Length;
    }
}
