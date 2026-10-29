namespace Easy.TimeSeries.Aggregation;

using Easy.TimeSeries.Abstractions;

/// <summary>
/// An aggregated file cannot be computed: a raw file does not match the spec, a row lies outside its raw file's
/// time span, a limit is exceeded, or a result cannot be stored in its encoding.
/// </summary>
public class AggregationException : TimeSeriesException
{
    public AggregationException(string message)
        : base(message)
    {
    }

    public AggregationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal AggregationException(
        string message,
        string? rawPath = null,
        string? column = null,
        string? function = null,
        Exception? innerException = null)
        : base(Compose(message, rawPath), innerException ?? new InvalidOperationException(message))
    {
        RawPath = rawPath;
        Column = column;
        Function = function;
    }

    /// <summary>Raw file the error relates to, when known.</summary>
    public string? RawPath { get; }

    /// <summary>Raw or aggregated column the error relates to, when known.</summary>
    public string? Column { get; }

    /// <summary>Aggregated column label of the function the error relates to, when known.</summary>
    public string? Function { get; }

    private static string Compose(string message, string? rawPath)
        => rawPath is null ? message : $"{message} (raw file '{rawPath}')";
}
