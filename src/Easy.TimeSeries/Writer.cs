namespace Easy.TimeSeries;

using Easy.TimeSeries.Storage;
using System.Collections.Immutable;

public sealed class Writer : IDisposable
{
    private readonly List<(ColumnInfo, PooledArrayBufferWriter)> columns = new(capacity: 4);
    private readonly int rows;
    private bool disposed;

    public Version Version { get; init; } = Version.V1;

    public float BufferGrowFactor { get; init; } = Constants.DefaultBufferGrowFactor;

    public int MaxAllowedBufferSize { get; init; } = Constants.MaxAllowedBufferSize;

    public Writer(int rows)
    {
        Expect.EqualOrGreaterThan(rows, 2);
        this.rows = rows;
    }

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

    public Writer AddTime(
        IEnumerable<DateTime> values,
        string columnLabel,
        TimePrecision precision = TimePrecision.Milliseconds)
    {
        var sizeHint = DateTimeWriter.GetSizeHint(rows, precision);
        var (bufferWriter, bitWriter) = CreateWriters(sizeHint);
        var writer = new DateTimeWriter(bitWriter, precision);
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
        var ci = new ColumnInfo(columns.Count, ColumnValueType.DateTime, (int)precision, columnLabel);
        columns.Add((ci, bufferWriter));

        return this;
    }

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

    private (PooledArrayBufferWriter bufferWriter, BitWriter bitWriter) CreateWriters(int sizeHint)
    {
        var bufferProvider = new PooledArrayBufferWriter(sizeHint, BufferGrowFactor, MaxAllowedBufferSize);
        bufferProvider.Advance(ColumnHeader.Size);
        var bitWriter = new BitWriter(bufferProvider, bufferProvider.GetColumnHeaderMemory);
        return (bufferProvider, bitWriter);
    }
}
