namespace Easy.TimeSeries;

using Easy.TimeSeries.Abstractions;
using System.Buffers;
using System.Runtime.CompilerServices;

/// <summary>
/// Decodes a serialized buffer column by column, dispatching each column to the reader for its
/// <see cref="ColumnValueType"/> and pushing the values into an <see cref="IMaterializer{T}"/> to build the rows.
/// The materializer decides which columns are mapped; <see cref="ReadOptions"/> controls projection and how
/// unmapped columns are handled.
/// </summary>
public static class Reader
{
    /// <summary>Reads the buffer from <paramref name="storage"/> and materializes it into <typeparamref name="T"/> instances.</summary>
    public static async Task<T[]> ReadFromAsync<T>(
        IReadStorage storage,
        IMaterializer<T> materializer,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var memory = await storage.ReadAsync(cancellationToken);
        return ReadFrom(memory.Span, materializer, options);
    }

    /// <summary>Materializes an in-memory buffer into <typeparamref name="T"/> instances without touching storage.</summary>
    public static T[] ReadFrom<T>(
        ReadOnlySpan<byte> buffer,
        IMaterializer<T> materializer,
        ReadOptions? options = null)
        where T : class, new()
    {
        options ??= new ReadOptions();
        var (_, columns) = Header.ReadLayout(buffer, options.ColumnIndexes);
        foreach (var (info, start, length) in columns)
        {
            var columnSpan = buffer.Slice(start, length);
            switch (info.ValueType)
            {
                case ColumnValueType.DateTimeOrdered:
                    ColumnDateTimeOrdered(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.TimeSpan:
                    ColumnTimeSpan(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.Float:
                    ColumnFloat(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.Double:
                    ColumnDouble(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.Decimal:
                    ColumnDecimal(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.Int32:
                    ColumnInt32(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.Int64:
                    ColumnInt64(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.Bool:
                    ColumnBool(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.ScaledNumber32:
                    ColumnScaledNumber32(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.ScaledNumber64:
                    ColumnScaledNumber64(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.Category:
                    ColumnCategory(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.DateTimeUnordered:
                    ColumnDateTimeUnordered(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.FloatRaw:
                    ColumnFloatRaw(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.DoubleRaw:
                    ColumnDoubleRaw(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.Int64Raw:
                    ColumnInt64Raw(columnSpan, info, materializer, options);
                    break;

                case ColumnValueType.Int32Raw:
                    ColumnInt32Raw(columnSpan, info, materializer, options);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        return materializer.GetResult();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnDateTimeOrdered<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var reader = new DateTimeOrderedReader(columnSpan, (TimePrecision)info.Meta);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnDateTimeUnordered<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var reader = new DateTimeUnorderedReader(columnSpan, (TimePrecision)info.Meta);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnTimeSpan<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var reader = new TimeSpanReader(columnSpan, (TimePrecision)info.Meta);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnFloat<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var reader = new FloatReader(columnSpan);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnDouble<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var reader = new DoubleReader(columnSpan);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnDecimal<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var reader = new DecimalReader(columnSpan);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnInt32<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var reader = new Int32Reader(columnSpan);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnInt64<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var reader = new Int64Reader(columnSpan);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnScaledNumber32<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var scale = (int)Math.Pow(10, info.Meta);
        var reader = new ScaledNumber32Reader(columnSpan, scale);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnScaledNumber64<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var scale = (int)Math.Pow(10, info.Meta);
        var reader = new ScaledNumber64Reader(columnSpan, scale);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnBool<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var reader = new BoolReader(columnSpan);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnCategory<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
    {
        var reader = new CategoryReader(columnSpan);
        var count = reader.ColumnHeader.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (bindingExists)
        {
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(reader.Read());
            }
        }
        else if (!options.IgnoreUnknownColumnIndexes)
        {
            throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnFloatRaw<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
        => ColumnRaw<T, float>(columnSpan, info, materializer, options);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnDoubleRaw<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
        => ColumnRaw<T, double>(columnSpan, info, materializer, options);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnInt64Raw<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
        => ColumnRaw<T, long>(columnSpan, info, materializer, options);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnInt32Raw<T>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
        => ColumnRaw<T, int>(columnSpan, info, materializer, options);

    private static void ColumnRaw<T, TValue>(
        ReadOnlySpan<byte> columnSpan,
        ColumnInfo info,
        IMaterializer<T> materializer,
        ReadOptions options)
        where T : class, new()
        where TValue : struct
    {
        if (!ColumnHeader.TryReadFrom(columnSpan, out var header))
        {
            throw new ArgumentException("Buffer contains invalid column header.", nameof(columnSpan));
        }

        var count = header.Records;
        var bindingExists = materializer.BeginColumn(info.Index, count);
        if (!bindingExists)
        {
            if (!options.IgnoreUnknownColumnIndexes)
            {
                throw TimeSeriesException.UnmappedColumnIndex(info.Index, typeof(T));
            }

            return;
        }

        var payload = columnSpan.Slice(ColumnHeader.Size, (int)header.DataLength);
        var pool = ArrayPool<TValue>.Shared;
        var array = pool.Rent(count);
        try
        {
            var values = array.AsSpan(0, count);
            Util.BrotliDecompress(payload, values);
            for (var i = 0; i < count; i++)
            {
                materializer.Hydrate(values[i]);
            }
        }
        finally
        {
            pool.Return(array);
        }
    }
}
