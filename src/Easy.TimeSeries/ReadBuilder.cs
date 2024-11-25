namespace Easy.TimeSeries;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Storage;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

public sealed class ReadBuilder
{
    public async Task<ImmutableArray<T>> ReadFromAsync<T>(
        IReadStorage storage,
        int[]? columnIndexes,
        IHydrator<T> deserializer,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var memory = await storage.ReadAsync(cancellationToken);
        return ReadFrom(memory.Span, columnIndexes, deserializer);
    }

    public Task<ImmutableArray<T>> ReadFromAsync<T>(
        IReadStorage storage,
        IHydrator<T> deserializer,
        CancellationToken cancellationToken = default)
        where T : class, new()
        => ReadFromAsync(storage, null, deserializer, cancellationToken);

    public ImmutableArray<T> ReadFrom<T>(
        ReadOnlySpan<byte> buffer,
        int[]? columnIndexes,
        IHydrator<T> deserializer)
        where T : class, new()
    {
        var (_, columns) = Header.ReadLayout(buffer, columnIndexes);
        foreach (var (info, start, length) in columns)
        {
            var columnSpan = buffer.Slice(start, length);
            switch (info.ValueType)
            {
                case ColumnValueType.DateTime:
                    ColumnDateTime(columnSpan, info, deserializer);
                    break;

                case ColumnValueType.TimeSpan:
                    ColumnTimeSpan(columnSpan, info, deserializer);
                    break;

                case ColumnValueType.Float:
                    ColumnFloat(columnSpan, info, deserializer);
                    break;

                case ColumnValueType.Double:
                    ColumnDouble(columnSpan, info, deserializer);
                    break;

                case ColumnValueType.Int32:
                    ColumnInt32(columnSpan, info, deserializer);
                    break;

                case ColumnValueType.Int64:
                    ColumnInt64(columnSpan, info, deserializer);
                    break;

                case ColumnValueType.Bool:
                    ColumnBool(columnSpan, info, deserializer);
                    break;

                case ColumnValueType.ScaledNumber32:
                    ColumnScaledNumber32(columnSpan, info, deserializer);
                    break;

                case ColumnValueType.ScaledNumber64:
                    ColumnScaledNumber64(columnSpan, info, deserializer);
                    break;

                case ColumnValueType.Category:
                    ColumnCategory(columnSpan, info, deserializer);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        return ImmutableArray.Create(deserializer.GetResult());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnDateTime<T>(ReadOnlySpan<byte> columnSpan, ColumnInfo info, IHydrator<T> deserializer)
        where T : class, new()
    {
        var reader = new DateTimeReader(columnSpan, (TimePrecision)info.Meta);
        for (var row = 0; row < reader.ColumnHeader.Records; row++)
        {
            deserializer.Hydrate(info.Index, row, reader.Read());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnTimeSpan<T>(ReadOnlySpan<byte> columnSpan, ColumnInfo info, IHydrator<T> deserializer)
        where T : class, new()
    {
        var reader = new TimeSpanReader(columnSpan, (TimePrecision)info.Meta);
        for (var row = 0; row < reader.ColumnHeader.Records; row++)
        {
            deserializer.Hydrate(info.Index, row, reader.Read());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnFloat<T>(ReadOnlySpan<byte> columnSpan, ColumnInfo info, IHydrator<T> deserializer)
        where T : class, new()
    {
        var reader = new FloatReader(columnSpan);
        for (var row = 0; row < reader.ColumnHeader.Records; row++)
        {
            deserializer.Hydrate(info.Index, row, reader.Read());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnDouble<T>(ReadOnlySpan<byte> columnSpan, ColumnInfo info, IHydrator<T> deserializer)
        where T : class, new()
    {
        var reader = new DoubleReader(columnSpan);
        for (var row = 0; row < reader.ColumnHeader.Records; row++)
        {
            deserializer.Hydrate(info.Index, row, reader.Read());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnInt32<T>(ReadOnlySpan<byte> columnSpan, ColumnInfo info, IHydrator<T> deserializer)
        where T : class, new()
    {
        var reader = new Int32Reader(columnSpan);
        for (var row = 0; row < reader.ColumnHeader.Records; row++)
        {
            deserializer.Hydrate(info.Index, row, reader.Read());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnInt64<T>(ReadOnlySpan<byte> columnSpan, ColumnInfo info, IHydrator<T> deserializer)
        where T : class, new()
    {
        var reader = new Int64Reader(columnSpan);
        for (var row = 0; row < reader.ColumnHeader.Records; row++)
        {
            deserializer.Hydrate(info.Index, row, reader.Read());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnScaledNumber32<T>(ReadOnlySpan<byte> columnSpan, ColumnInfo info, IHydrator<T> deserializer)
        where T : class, new()
    {
        var reader = new ScaledNumber32Reader(columnSpan, info.Meta);
        for (var row = 0; row < reader.ColumnHeader.Records; row++)
        {
            deserializer.Hydrate(info.Index, row, reader.Read());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnScaledNumber64<T>(ReadOnlySpan<byte> columnSpan, ColumnInfo info, IHydrator<T> deserializer)
        where T : class, new()
    {
        var reader = new ScaledNumber64Reader(columnSpan, info.Meta);
        for (var row = 0; row < reader.ColumnHeader.Records; row++)
        {
            deserializer.Hydrate(info.Index, row, reader.Read());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnBool<T>(ReadOnlySpan<byte> columnSpan, ColumnInfo info, IHydrator<T> deserializer)
        where T : class, new()
    {
        var reader = new BoolReader(columnSpan);
        for (var row = 0; row < reader.ColumnHeader.Records; row++)
        {
            deserializer.Hydrate(info.Index, row, reader.Read());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColumnCategory<T>(ReadOnlySpan<byte> columnSpan, ColumnInfo info, IHydrator<T> deserializer)
        where T : class, new()
    {
        var reader = new CategoryReader(columnSpan);
        for (var row = 0; row < reader.ColumnHeader.Records; row++)
        {
            deserializer.Hydrate(info.Index, row, reader.Read());
        }
    }
}
