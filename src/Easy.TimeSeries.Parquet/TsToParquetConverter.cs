namespace Easy.TimeSeries.Parquet;

using Easy.TimeSeries;
using Easy.TimeSeries.Abstractions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using global::Parquet;
using global::Parquet.Schema;

/// <summary>
/// Converts a serialized time-series buffer (this library's columnar format) into an Apache Parquet stream. Each
/// time-series column becomes one Parquet column using a native Parquet type; the mapping and options are described
/// in <c>docs/parquet-conversion.md</c>. Conversion is column-oriented and reuses the core reader, so no schema or DTO
/// needs to be supplied - the self-describing time-series header drives the whole process.
/// </summary>
public static class TsToParquetConverter
{
    /// <summary>
    /// Reads a time-series buffer from <paramref name="source"/> and writes the Parquet result to
    /// <paramref name="destination"/>, which must be writable and seekable.
    /// </summary>
    public static async Task ConvertAsync(
        IReadStorage source,
        Stream destination,
        ParquetConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var buffer = await source.ReadAsync(cancellationToken).ConfigureAwait(false);
        await ConvertAsync(buffer, destination, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Converts an in-memory time-series buffer and writes the Parquet result to <paramref name="destination"/>,
    /// which must be writable and seekable.
    /// </summary>
    public static async Task ConvertAsync(
        ReadOnlyMemory<byte> buffer,
        Stream destination,
        ParquetConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        options ??= new ParquetConversionOptions();

        var (header, _) = Header.ReadLayout(buffer.Span);
        var columns = header.Columns;

        var fields = new DataField[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            fields[i] = ParquetTypeMapper.Map(columns[i], options);
        }

        var schema = new ParquetSchema(fields);
        var sink = new ParquetColumnSink(columns);

        // Decodes every column into the sink's per-column arrays via the existing reader dispatch.
        Reader.ReadFrom(buffer.Span, sink);

        await using var writer = await ParquetWriter
            .CreateAsync(schema, destination, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var totalRows = sink.RowCount;
        var groupSize = options.RowGroupSize is int size and > 0 ? size : totalRows;

        var start = 0;
        do
        {
            var length = Math.Min(groupSize, totalRows - start);
            using var rowGroup = writer.CreateRowGroup();
            for (var i = 0; i < columns.Length; i++)
            {
                await sink.WriteColumnAsync(rowGroup, i, fields[i], start, length).ConfigureAwait(false);
            }

            start += length;
        }
        while (start < totalRows);
    }
}
