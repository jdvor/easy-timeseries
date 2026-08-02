namespace Easy.TimeSeries.Parquet;

using Easy.TimeSeries;
using System;
using global::Parquet.Schema;

/// <summary>
/// Maps a time-series <see cref="ColumnInfo"/> to the Parquet <see cref="DataField"/> that best represents it, using
/// native Parquet types. The full mapping is documented in <c>docs/parquet-conversion.md</c>.
/// </summary>
internal static class ParquetTypeMapper
{
    public static DataField Map(ColumnInfo info, ParquetConversionOptions options)
    {
        var name = string.IsNullOrEmpty(info.Label) ? $"col{info.Index}" : info.Label;
        return info.ValueType switch
        {
            // TimePrecision tops out at milliseconds, so the INT64 millisecond TIMESTAMP covers every case.
            ColumnValueType.DateTimeOrdered or ColumnValueType.DateTimeUnordered
                => new DateTimeDataField(name, DateTimeFormat.Timestamp),

            // Stored as INT64 ticks (100 ns); no native Parquet interval type carries sub-day precision losslessly.
            ColumnValueType.TimeSpan => new DataField<long>(name),

            ColumnValueType.Float or ColumnValueType.FloatRaw or ColumnValueType.ScaledNumber32
                => new DataField<float>(name),

            ColumnValueType.Double or ColumnValueType.DoubleRaw or ColumnValueType.ScaledNumber64
                => new DataField<double>(name),

            ColumnValueType.Decimal => new DecimalDataField(name, options.DecimalPrecision, options.DecimalScale),

            ColumnValueType.Int32 or ColumnValueType.Int32Raw => new DataField<int>(name),
            ColumnValueType.Int64 or ColumnValueType.Int64Raw => new DataField<long>(name),
            ColumnValueType.Bool => new DataField<bool>(name),
            ColumnValueType.Category => new DataField<string>(name),

            _ => throw new NotSupportedException(
                $"Column type '{info.ValueType}' cannot be mapped to a Parquet type."),
        };
    }
}
