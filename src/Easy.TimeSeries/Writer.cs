namespace Easy.TimeSeries;

using Easy.TimeSeries.Abstractions;
using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

/// <summary>
/// A builder for time series data, which accumulates data in memory by receiving series of values representing columns
/// and at the end writes it all to a storage.
/// </summary>
public sealed class Writer : IDisposable
{
    private readonly List<(ColumnInfo, PooledArrayBufferWriter)> columns = new(capacity: 4);
    private readonly int rows;
    private bool disposed;

    /// <summary>File format version stamped into the header.</summary>
    public Version Version { get; init; } = Version.V1;

    /// <summary>Fraction a per-column buffer grows by when it runs out of space (e.g. 0.5 = grow by half).</summary>
    public float BufferGrowFactor { get; init; } = Constants.DefaultBufferGrowFactor;

    /// <summary>Upper bound on how large a single column buffer may grow before writing throws, as a guard against runaway allocation.</summary>
    public int MaxAllowedBufferSize { get; init; } = Constants.MaxAllowedBufferSize;

    /// <summary>Creates a writer for a fixed number of rows. Every <c>Add*</c> call reads at most this many values.</summary>
    /// <param name="rows">Row count shared by all columns; must be at least 2.</param>
    public Writer(int rows)
    {
        Expect.EqualOrGreaterThan(rows, 2);
        this.rows = rows;
    }

    /// <summary>Returns all pooled column buffers to the array pool.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var (_, bufferWriter) in columns)
        {
            bufferWriter.Dispose();
        }
    }

    /// <summary>
    /// Adds a <see cref="DateTime"/> column whose values MUST be UTC and sorted in ascending order
    /// (monotonically non-decreasing) - writing throws otherwise. This is the preferred column type
    /// for time series timestamps; it compresses far better than <see cref="AddTimeUnordered"/>.
    /// </summary>
    public Writer AddTimeOrdered(
        IEnumerable<DateTime> values,
        string columnLabel,
        TimePrecision precision = TimePrecision.Milliseconds)
    {
        var sizeHint = DateTimeOrderedWriter.GetSizeHint(rows, precision);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new DateTimeOrderedWriter(bitWriter, precision);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.DateTimeOrdered, (int)precision, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>
    /// Adds a <see cref="DateTime"/> column whose values MUST be UTC but may appear in any order,
    /// repeat, or precede 2000-01-01. Prefer <see cref="AddTimeOrdered"/> when the values are
    /// sorted in ascending order - it compresses considerably better.
    /// </summary>
    public Writer AddTimeUnordered(
        IEnumerable<DateTime> values,
        string columnLabel,
        TimePrecision precision = TimePrecision.Milliseconds)
    {
        var sizeHint = DateTimeUnorderedWriter.GetSizeHint(rows, precision);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new DateTimeUnorderedWriter(bitWriter, precision);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.DateTimeUnordered, (int)precision, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>Adds a <see cref="TimeSpan"/> column. Intervals are truncated to <paramref name="precision"/> before encoding.</summary>
    public Writer AddInterval(
        IEnumerable<TimeSpan> values,
        string columnLabel,
        TimePrecision precision = TimePrecision.Milliseconds)
    {
        var sizeHint = TimeSpanWriter.GetSizeHint(rows, precision);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new TimeSpanWriter(bitWriter, precision);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.TimeSpan, (int)precision, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>
    /// Adds a <see cref="float"/> column stored losslessly with XOR/delta encoding. Best for slowly-varying signals.
    /// For uncorrelated values use <see cref="AddFloatRandom"/>; to trade precision for size use <see cref="AddScaledNumber32"/>.
    /// </summary>
    public Writer AddFloat(IEnumerable<float> values, string columnLabel)
    {
        var sizeHint = FloatWriter.GetSizeHint(rows);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new FloatWriter(bitWriter);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.Float, 0, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>
    /// Adds a <see cref="float"/> column whose values are uncorrelated between rows (e.g. geographic
    /// coordinates). Values are stored raw and Brotli-compressed instead of XOR/delta encoded - prefer
    /// <see cref="AddFloat"/> for slowly-varying signals, where XOR/delta compresses far better.
    /// </summary>
    public Writer AddFloatRandom(IEnumerable<float> values, string columnLabel)
    {
        AddRawColumn(values, columnLabel, ColumnValueType.FloatRaw);
        return this;
    }

    /// <summary>
    /// Adds a <see cref="double"/> column whose values are uncorrelated between rows. Values are stored raw
    /// and Brotli-compressed instead of XOR/delta encoded - prefer <see cref="AddDouble"/> for
    /// slowly-varying signals.
    /// </summary>
    public Writer AddDoubleRandom(IEnumerable<double> values, string columnLabel)
    {
        AddRawColumn(values, columnLabel, ColumnValueType.DoubleRaw);
        return this;
    }

    /// <summary>
    /// Adds a <see cref="long"/> column whose values are uncorrelated between rows. Values are stored raw
    /// and Brotli-compressed instead of XOR/delta encoded - prefer <see cref="AddInt64"/> for
    /// slowly-varying signals.
    /// </summary>
    public Writer AddInt64Random(IEnumerable<long> values, string columnLabel)
    {
        AddRawColumn(values, columnLabel, ColumnValueType.Int64Raw);
        return this;
    }

    /// <summary>
    /// Adds an <see cref="int"/> column whose values are uncorrelated between rows. Values are stored raw
    /// and Brotli-compressed instead of XOR/delta encoded - prefer <see cref="AddInt32"/> for
    /// slowly-varying signals.
    /// </summary>
    public Writer AddInt32Random(IEnumerable<int> values, string columnLabel)
    {
        AddRawColumn(values, columnLabel, ColumnValueType.Int32Raw);
        return this;
    }

    /// <summary>
    /// Adds a <see cref="float"/> column quantized to <paramref name="decimalPlaces"/> and stored as a scaled 32-bit
    /// integer (lossy). Compresses far better than <see cref="AddFloat"/> when the meaningful precision is a small,
    /// fixed number of decimals. The scaled value must fit in <see cref="int"/>; use <see cref="AddScaledNumber64"/> otherwise.
    /// </summary>
    /// <param name="decimalPlaces">Decimal places to retain, 1-8.</param>
    public Writer AddScaledNumber32(IEnumerable<float> values, string columnLabel, int decimalPlaces)
    {
        Expect.Range(decimalPlaces, 1, 8);

        var scale = (int)Math.Pow(10, decimalPlaces);
        var sizeHint = ScaledNumber32Writer.GetSizeHint(rows);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new ScaledNumber32Writer(bitWriter, scale);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.ScaledNumber32, decimalPlaces, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>
    /// Adds a <see cref="double"/> column stored losslessly with XOR/delta encoding. Best for slowly-varying signals.
    /// For uncorrelated values use <see cref="AddDoubleRandom"/>; to trade precision for size use <see cref="AddScaledNumber64"/>.
    /// </summary>
    public Writer AddDouble(IEnumerable<double> values, string columnLabel)
    {
        var sizeHint = DoubleWriter.GetSizeHint(rows);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new DoubleWriter(bitWriter);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.Double, 0, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>Adds a <see cref="decimal"/> column stored losslessly, including scale (so <c>1.0m</c> and <c>1.00m</c> round-trip distinctly).</summary>
    public Writer AddDecimal(IEnumerable<decimal> values, string columnLabel)
    {
        var sizeHint = DecimalWriter.GetSizeHint(rows);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new DecimalWriter(bitWriter);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.Decimal, 0, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>
    /// Adds a <see cref="double"/> column quantized to <paramref name="decimalPlaces"/> and stored as a scaled 64-bit
    /// integer (lossy). The wider counterpart to <see cref="AddScaledNumber32"/> for values whose scaled magnitude
    /// exceeds <see cref="int"/>.
    /// </summary>
    /// <param name="decimalPlaces">Decimal places to retain, 1-8.</param>
    public Writer AddScaledNumber64(IEnumerable<double> values, string columnLabel, int decimalPlaces)
    {
        Expect.Range(decimalPlaces, 1, 8);

        var scale = (int)Math.Pow(10, decimalPlaces);
        var sizeHint = ScaledNumber64Writer.GetSizeHint(rows);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new ScaledNumber64Writer(bitWriter, scale);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.ScaledNumber64, decimalPlaces, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>
    /// Adds an <see cref="int"/> column stored losslessly with XOR/delta encoding. Best when successive values are
    /// similar; for uncorrelated values use <see cref="AddInt32Random"/>.
    /// </summary>
    public Writer AddInt32(IEnumerable<int> values, string columnLabel)
    {
        var sizeHint = Int32Writer.GetSizeHint(rows);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new Int32Writer(bitWriter);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.Int32, 0, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>
    /// Adds a <see cref="long"/> column stored losslessly with XOR/delta encoding. Best when successive values are
    /// similar; for uncorrelated values use <see cref="AddInt64Random"/>.
    /// </summary>
    public Writer AddInt64(IEnumerable<long> values, string columnLabel)
    {
        var sizeHint = Int64Writer.GetSizeHint(rows);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new Int64Writer(bitWriter);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.Int64, 0, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>Adds a <see cref="bool"/> column packed one bit per value.</summary>
    public Writer AddBool(IEnumerable<bool> values, string columnLabel)
    {
        var sizeHint = BoolWriter.GetSizeHint(rows);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new BoolWriter(bitWriter);
        var i = 0;
        foreach (var value in values)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(value);
            ++i;
        }

        bitWriter.Flush();
        var ci = new ColumnInfo(columns.Count, ColumnValueType.Bool, 0, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>
    /// Adds a string column stored as dictionary-encoded ids plus a label map. Ideal for low-cardinality, repeated
    /// text (categories, tags, statuses); poorly suited to high-cardinality free text.
    /// </summary>
    public Writer AddCategory(IEnumerable<string> values, string columnLabel)
    {
        var accumulator = new IdAccumulator(rows);
        accumulator.AddRange(values);
        var (keys, ids) = accumulator.BuildIterator();
        var categoryMap = new CategoryMap(keys);
        var sizeHint = CategoryWriter.GetSizeHint(rows) + categoryMap.SizeHint;
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new CategoryWriter(bitWriter);
        var i = 0;
        foreach (var id in ids)
        {
            if (i >= rows)
            {
                break;
            }

            writer.Write(id);
            ++i;
        }

        bitWriter.Flush();
        categoryMap.WriteTo(bufferWriter);

        var ci = new ColumnInfo(columns.Count, ColumnValueType.Category, 0, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

    /// <summary>Writes the header and every accumulated column to <paramref name="storage"/>, then closes it.</summary>
    public async Task WriteToAsync(IWriteStorage storage, CancellationToken cancellationToken = default)
    {
        await WriteHeaderAsync(storage, cancellationToken).ConfigureAwait(false);
        foreach (var (_, bufferWriter) in columns)
        {
            var memory = bufferWriter.WrittenMemory;
            await storage.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
        }

        await storage.CloseAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task WriteHeaderAsync(IWriteStorage storage, CancellationToken cancellationToken)
    {
        var headerColumns = columns.Select(x => x.Item1);
        var storageHeader = new Header(Version, ImmutableArray.CreateRange(headerColumns));
        var buffer = new byte[storageHeader.SizeHint];
        var written = storageHeader.WriteTo(buffer);
        var memory = new ReadOnlyMemory<byte>(buffer)[..written];
        return storage.WriteAsync(memory, cancellationToken);
    }

    private void AddRawColumn<T>(IEnumerable<T> values, string columnLabel, ColumnValueType valueType)
        where T : struct
    {
        var pool = ArrayPool<T>.Shared;
        var array = pool.Rent(rows);
        try
        {
            var count = 0;
            foreach (var value in values)
            {
                if (count >= rows)
                {
                    break;
                }

                array[count++] = value;
            }

            var src = MemoryMarshal.AsBytes(array.AsSpan(0, count));
            var bufferWriter = Util.BrotliCompress(src, count, BufferGrowFactor, MaxAllowedBufferSize);
            var ci = new ColumnInfo(columns.Count, valueType, 0, columnLabel);
            columns.Add((ci, bufferWriter));
        }
        finally
        {
            pool.Return(array);
        }
    }

    private (PooledArrayBufferWriter bufferWriter, BitWriter bitWriter) CreateWriters(int sizeHint)
    {
        var bufferProvider = new PooledArrayBufferWriter(sizeHint + ColumnHeader.Size, BufferGrowFactor, MaxAllowedBufferSize);
        bufferProvider.Advance(ColumnHeader.Size);
        var bitWriter = new BitWriter(bufferProvider, bufferProvider.GetColumnHeaderMemory);
        return (bufferProvider, bitWriter);
    }
}
