namespace Easy.TimeSeries;

using Easy.TimeSeries.Abstractions;
using System.Runtime.CompilerServices;

public static class Reader
{
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
}
