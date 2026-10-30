namespace Easy.TimeSeries.Aggregation.Functions;

using System.Numerics;

/// <summary>
/// Quantile of one numeric column, skipping NaN. Linear interpolation between the two closest ranks
/// (Hyndman-Fan type 7, the default of NumPy and Excel <c>PERCENTILE.INC</c>), so level 0.5 is the median.
/// </summary>
/// <remarks>Decimal columns use <see cref="QuantileDecimal"/>, which stays exact.</remarks>
public sealed class Quantile<TIn> : IAggregationFunction<double>
    where TIn : unmanaged, INumber<TIn>
{
    private readonly double p;

    public Quantile(string column, double p, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(column), p, label ?? FunctionLabels.Of(column, Suffix(p)), output)
    {
    }

    public Quantile(int columnIndex, double p, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(columnIndex), p, label ?? FunctionLabels.Of(columnIndex, Suffix(p)), output)
    {
    }

    private Quantile(AggregationInput input, double p, string label, OutputEncoding? output)
    {
        FunctionLabels.RejectDecimal<TIn>("Quantile", nameof(QuantileDecimal));
        this.p = FunctionLabels.CheckQuantileLevel(p);
        Inputs = [input];
        Name = label;
        Output = output ?? OutputEncoding.FromInputPrecision;
    }

    public string Name { get; }

    public IReadOnlyList<AggregationInput> Inputs { get; }

    public OutputEncoding Output { get; }

    /// <summary>Quantile level in [0, 1].</summary>
    public double Level => p;

    public double Compute(in AggregationSlice slice)
    {
        var sorted = slice.Sorted<TIn>(0);
        if (sorted.IsEmpty)
        {
            return double.NaN;
        }

        var h = (sorted.Length - 1) * p;
        var lo = (int)Math.Floor(h);
        var hi = Math.Min(lo + 1, sorted.Length - 1);
        var a = double.CreateChecked(sorted[lo]);
        var fraction = h - lo;
        if (fraction == 0 || lo == hi)
        {
            return a;
        }

        var b = double.CreateChecked(sorted[hi]);

        // Equal neighbors return as is, so infinities never become NaN through inf - inf.
        return a == b ? a : Interpolate(a, b, fraction);
    }

    /// <summary>
    /// <c>a + fraction x (b - a)</c>, exact at fraction 0 and monotone. For opposite signs b - a can overflow, while
    /// each term of the weighted form stays within range.
    /// </summary>
    internal static double Interpolate(double a, double b, double fraction)
        => a < 0 && b > 0 ? ((1 - fraction) * a) + (fraction * b) : a + (fraction * (b - a));

    private static string Suffix(double p) => FunctionLabels.QuantileSuffix(FunctionLabels.CheckQuantileLevel(p));
}

/// <summary>Quantile of a <see cref="decimal"/> column, interpolated exactly in decimal.</summary>
public sealed class QuantileDecimal : IAggregationFunction<decimal>
{
    private readonly decimal p;

    public QuantileDecimal(string column, double p, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<decimal>(column), p, label ?? FunctionLabels.Of(column, Suffix(p)), output)
    {
    }

    public QuantileDecimal(int columnIndex, double p, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<decimal>(columnIndex), p, label ?? FunctionLabels.Of(columnIndex, Suffix(p)), output)
    {
    }

    private QuantileDecimal(AggregationInput input, double p, string label, OutputEncoding? output)
    {
        this.p = (decimal)FunctionLabels.CheckQuantileLevel(p);
        Inputs = [input];
        Name = label;
        Output = output ?? OutputEncoding.FromInputPrecision;
    }

    public string Name { get; }

    public IReadOnlyList<AggregationInput> Inputs { get; }

    public OutputEncoding Output { get; }

    public decimal Compute(in AggregationSlice slice)
    {
        var sorted = slice.Sorted<decimal>(0);
        var h = (sorted.Length - 1) * p;
        var lo = (int)decimal.Floor(h);
        var hi = Math.Min(lo + 1, sorted.Length - 1);
        var fraction = h - lo;
        if (fraction == 0 || lo == hi)
        {
            return sorted[lo];
        }

        // Opposite signs: the difference can overflow decimal, which throws; each weighted term cannot.
        var (a, b) = (sorted[lo], sorted[hi]);
        return a < 0 && b > 0 ? ((1 - fraction) * a) + (fraction * b) : a + (fraction * (b - a));
    }

    private static string Suffix(double p) => FunctionLabels.QuantileSuffix(FunctionLabels.CheckQuantileLevel(p));
}
