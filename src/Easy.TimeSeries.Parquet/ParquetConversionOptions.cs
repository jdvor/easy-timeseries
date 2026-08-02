namespace Easy.TimeSeries.Parquet;

/// <summary>
/// Optional settings controlling how a time-series buffer is converted to Apache Parquet. Every property has a sane
/// default, so a conversion works without supplying any options.
/// </summary>
public sealed record ParquetConversionOptions
{
    /// <summary>Total number of digits used for <c>Decimal</c> columns mapped to the Parquet DECIMAL logical type.</summary>
    public int DecimalPrecision { get; init; } = 38;

    /// <summary>Number of fractional digits used for <c>Decimal</c> columns mapped to the Parquet DECIMAL logical type.</summary>
    public int DecimalScale { get; init; } = 18;

    /// <summary>
    /// When set to a positive value, rows are split into Parquet row groups of at most this many rows.
    /// <c>null</c> (the default) writes all rows in a single row group.
    /// </summary>
    public int? RowGroupSize { get; init; }
}
