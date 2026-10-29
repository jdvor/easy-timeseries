namespace Easy.TimeSeries.Aggregation;

using Easy.TimeSeries.Aggregation.Functions;

/// <summary>
/// Predefined single-column aggregations for <see cref="AggregationSpec.Column"/>. Each binds to the matching class
/// in <see cref="Functions"/> once the raw column type is known.
/// </summary>
public static class Agg
{
    /// <summary>Arithmetic mean; numeric columns. Label <c>{column}.Avg</c>.</summary>
    public static AggregationDescriptor Avg { get; } = new AvgDescriptor();

    /// <summary>Median (quantile 0.5); numeric columns. Label <c>{column}.Median</c>.</summary>
    public static AggregationDescriptor Median { get; } = new QuantileDescriptor(0.5, "Median");

    /// <summary>Quantile 0.95; numeric columns. Label <c>{column}.Q95</c>.</summary>
    public static AggregationDescriptor Q95 { get; } = new QuantileDescriptor(0.95, "Q95");

    /// <summary>Quantile 0.99; numeric columns. Label <c>{column}.Q99</c>.</summary>
    public static AggregationDescriptor Q99 { get; } = new QuantileDescriptor(0.99, "Q99");

    /// <summary>Smallest value; numeric, DateTime and TimeSpan columns. Label <c>{column}.Min</c>.</summary>
    public static AggregationDescriptor Min { get; } = new MinDescriptor();

    /// <summary>Largest value; numeric, DateTime and TimeSpan columns. Label <c>{column}.Max</c>.</summary>
    public static AggregationDescriptor Max { get; } = new MaxDescriptor();

    /// <summary>Population standard deviation; numeric columns. Label <c>{column}.StdDev</c>.</summary>
    public static AggregationDescriptor StdDev { get; } = new StdDevDescriptor();

    /// <summary>Share of true values; Bool columns. Label <c>{column}.TrueRatio</c>.</summary>
    public static AggregationDescriptor TrueRatio { get; } = new TrueRatioDescriptor();

    /// <summary>Value of the latest row; any column. Label <c>{column}.Last</c>.</summary>
    public static AggregationDescriptor Last { get; } = new LastDescriptor();

    /// <summary>Quantile at level <paramref name="p"/> in [0, 1]; numeric columns. Label e.g. <c>{column}.Q99.9</c>.</summary>
    public static AggregationDescriptor Quantile(double p)
    {
        FunctionLabels.CheckQuantileLevel(p);
        return new QuantileDescriptor(p, FunctionLabels.QuantileSuffix(p));
    }

    /// <summary>The default for a column named without functions: Avg, TrueRatio or Last by type.</summary>
    internal static AggregationDescriptor Default { get; } = new DefaultDescriptor();

    private sealed class AvgDescriptor : AggregationDescriptor
    {
        protected override string Suffix => "Avg";

        protected override IAggregationFunction Create(string column, Type inputType, string label, OutputEncoding? output)
            => inputType == typeof(float) ? new Avg<float>(column, label, output)
                : inputType == typeof(double) ? new Avg<double>(column, label, output)
                : inputType == typeof(int) ? new Avg<int>(column, label, output)
                : inputType == typeof(long) ? new Avg<long>(column, label, output)
                : inputType == typeof(decimal) ? new AvgDecimal(column, label, output)
                : throw Unsupported(column, inputType);
    }

    private sealed class QuantileDescriptor(double p, string suffix) : AggregationDescriptor
    {
        protected override string Suffix => suffix;

        protected override IAggregationFunction Create(string column, Type inputType, string label, OutputEncoding? output)
            => inputType == typeof(float) ? new Quantile<float>(column, p, label, output)
                : inputType == typeof(double) ? new Quantile<double>(column, p, label, output)
                : inputType == typeof(int) ? new Quantile<int>(column, p, label, output)
                : inputType == typeof(long) ? new Quantile<long>(column, p, label, output)
                : inputType == typeof(decimal) ? new QuantileDecimal(column, p, label, output)
                : throw Unsupported(column, inputType);
    }

    private sealed class StdDevDescriptor : AggregationDescriptor
    {
        protected override string Suffix => "StdDev";

        protected override IAggregationFunction Create(string column, Type inputType, string label, OutputEncoding? output)
            => inputType == typeof(float) ? new StdDev<float>(column, label, output)
                : inputType == typeof(double) ? new StdDev<double>(column, label, output)
                : inputType == typeof(int) ? new StdDev<int>(column, label, output)
                : inputType == typeof(long) ? new StdDev<long>(column, label, output)
                : inputType == typeof(decimal) ? new StdDev<decimal>(column, label, output)
                : throw Unsupported(column, inputType);
    }

    private sealed class MinDescriptor : AggregationDescriptor
    {
        protected override string Suffix => "Min";

        protected override IAggregationFunction Create(string column, Type inputType, string label, OutputEncoding? output)
            => inputType == typeof(float) ? new Min<float>(column, label, output)
                : inputType == typeof(double) ? new Min<double>(column, label, output)
                : inputType == typeof(int) ? new Min<int>(column, label, output)
                : inputType == typeof(long) ? new Min<long>(column, label, output)
                : inputType == typeof(decimal) ? new Min<decimal>(column, label, output)
                : inputType == typeof(DateTime) ? new Min<DateTime>(column, label, output)
                : inputType == typeof(TimeSpan) ? new Min<TimeSpan>(column, label, output)
                : throw Unsupported(column, inputType);
    }

    private sealed class MaxDescriptor : AggregationDescriptor
    {
        protected override string Suffix => "Max";

        protected override IAggregationFunction Create(string column, Type inputType, string label, OutputEncoding? output)
            => inputType == typeof(float) ? new Max<float>(column, label, output)
                : inputType == typeof(double) ? new Max<double>(column, label, output)
                : inputType == typeof(int) ? new Max<int>(column, label, output)
                : inputType == typeof(long) ? new Max<long>(column, label, output)
                : inputType == typeof(decimal) ? new Max<decimal>(column, label, output)
                : inputType == typeof(DateTime) ? new Max<DateTime>(column, label, output)
                : inputType == typeof(TimeSpan) ? new Max<TimeSpan>(column, label, output)
                : throw Unsupported(column, inputType);
    }

    private sealed class TrueRatioDescriptor : AggregationDescriptor
    {
        protected override string Suffix => "TrueRatio";

        protected override IAggregationFunction Create(string column, Type inputType, string label, OutputEncoding? output)
            => inputType == typeof(bool) ? new TrueRatio(column, label, output) : throw Unsupported(column, inputType);
    }

    private sealed class LastDescriptor : AggregationDescriptor
    {
        protected override string Suffix => "Last";

        protected override IAggregationFunction Create(string column, Type inputType, string label, OutputEncoding? output)
            => inputType == typeof(float) ? new Last<float>(column, label, output)
                : inputType == typeof(double) ? new Last<double>(column, label, output)
                : inputType == typeof(int) ? new Last<int>(column, label, output)
                : inputType == typeof(long) ? new Last<long>(column, label, output)
                : inputType == typeof(decimal) ? new Last<decimal>(column, label, output)
                : inputType == typeof(DateTime) ? new Last<DateTime>(column, label, output)
                : inputType == typeof(TimeSpan) ? new Last<TimeSpan>(column, label, output)
                : inputType == typeof(bool) ? new Last<bool>(column, label, output)
                : inputType == typeof(string) ? new Last<string>(column, label, output)
                : throw Unsupported(column, inputType);
    }

    private sealed class DefaultDescriptor : AggregationDescriptor
    {
        protected override string Suffix => "Default";

        internal override bool IsTypeDependent => true;

        internal override AggregationDescriptor Resolve(string column, Type inputType)
        {
            if (ClrTypes.IsNumeric(inputType))
            {
                return CarryOver(Avg);
            }

            if (inputType == typeof(bool))
            {
                return CarryOver(TrueRatio);
            }

            if (inputType == typeof(string))
            {
                return CarryOver(Last);
            }

            throw new AggregationException(
                $"Column '{column}' of type {inputType.Name} has no default aggregation; name the functions explicitly.",
                column: column);
        }

        protected override IAggregationFunction Create(string column, Type inputType, string label, OutputEncoding? output)
            => throw Unsupported(column, inputType);
    }
}
