namespace Easy.TimeSeries.Aggregation.Functions;

using System.Runtime.CompilerServices;

/// <summary>Smallest value of one column, skipping NaN; keeps the raw column encoding.</summary>
public sealed class Min<TIn> : IAggregationFunction<TIn>
    where TIn : IComparable<TIn>
{
    public Min(string column, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(column), label ?? FunctionLabels.Of(column, "Min"), output)
    {
    }

    public Min(int columnIndex, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(columnIndex), label ?? FunctionLabels.Of(columnIndex, "Min"), output)
    {
    }

    private Min(AggregationInput input, string label, OutputEncoding? output)
    {
        Inputs = [input];
        Name = label;
        Output = output ?? OutputEncoding.SameAsInput;
    }

    public string Name { get; }

    public IReadOnlyList<AggregationInput> Inputs { get; }

    public OutputEncoding Output { get; }

    public TIn Compute(in AggregationSlice slice) => Extreme<TIn>.Find(slice.Values<TIn>(0), sign: -1);
}

/// <summary>Largest value of one column, skipping NaN; keeps the raw column encoding.</summary>
public sealed class Max<TIn> : IAggregationFunction<TIn>
    where TIn : IComparable<TIn>
{
    public Max(string column, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(column), label ?? FunctionLabels.Of(column, "Max"), output)
    {
    }

    public Max(int columnIndex, string? label = null, OutputEncoding? output = null)
        : this(AggregationInput.Of<TIn>(columnIndex), label ?? FunctionLabels.Of(columnIndex, "Max"), output)
    {
    }

    private Max(AggregationInput input, string label, OutputEncoding? output)
    {
        Inputs = [input];
        Name = label;
        Output = output ?? OutputEncoding.SameAsInput;
    }

    public string Name { get; }

    public IReadOnlyList<AggregationInput> Inputs { get; }

    public OutputEncoding Output { get; }

    public TIn Compute(in AggregationSlice slice) => Extreme<TIn>.Find(slice.Values<TIn>(0), sign: 1);
}

internal static class Extreme<T>
    where T : IComparable<T>
{
    /// <summary>The extreme value in the direction of <paramref name="sign"/>; NaN only when every value is NaN.</summary>
    public static T Find(ReadOnlySpan<T> values, int sign)
    {
        var best = values[0];
        var found = false;
        foreach (var v in values)
        {
            if (IsNaN(v))
            {
                continue;
            }

            if (!found || Math.Sign(Compare(v, best)) == sign)
            {
                best = v;
                found = true;
            }
        }

        return best;
    }

    private static int Compare(T a, T b)
        => typeof(T) == typeof(string)
            ? string.CompareOrdinal(Unsafe.As<T, string>(ref a), Unsafe.As<T, string>(ref b))
            : a.CompareTo(b);

    private static bool IsNaN(T v)
        => (typeof(T) == typeof(double) && double.IsNaN(Unsafe.As<T, double>(ref v)))
           || (typeof(T) == typeof(float) && float.IsNaN(Unsafe.As<T, float>(ref v)));
}
