namespace Easy.TimeSeries.Aggregation.Functions;

using System.Globalization;

/// <summary>Generated labels of predefined functions: <c>{column}.{suffix}</c>.</summary>
internal static class FunctionLabels
{
    public static string Of(string column, string suffix) => $"{column}.{suffix}";

    public static string Of(int columnIndex, string suffix) => $"#{columnIndex}.{suffix}";

    /// <summary>
    /// <c>Q{p x 100}</c>, computed in <see cref="decimal"/> so binary floating-point noise never reaches the label
    /// (in double, 0.07 x 100 is 7.000000000000001), invariant culture, no trailing zeros: <c>Q7</c>, <c>Q99.9</c>.
    /// </summary>
    public static string QuantileSuffix(double p)
        => $"Q{((decimal)p * 100m).ToString("0.############################", CultureInfo.InvariantCulture)}";

    public static double CheckQuantileLevel(double p)
        => p is >= 0d and <= 1d
            ? p
            : throw new ArgumentOutOfRangeException(nameof(p), p, "Quantile level must lie in [0, 1].");

    public static void RejectDecimal<TIn>(string function, string replacement)
    {
        if (typeof(TIn) == typeof(decimal))
        {
            throw new ArgumentException(
                $"{function}<decimal> is not supported; use {replacement}, which stays exact.",
                nameof(TIn));
        }
    }
}
