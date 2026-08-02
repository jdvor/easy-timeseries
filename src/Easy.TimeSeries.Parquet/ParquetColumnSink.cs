namespace Easy.TimeSeries.Parquet;

using Easy.TimeSeries;
using Easy.TimeSeries.Abstractions;
using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Parquet;
using global::Parquet.Schema;

/// <summary>
/// An <see cref="IMaterializer{T}"/> that, instead of building DTOs, accumulates each decoded column into a
/// strongly-typed array ready to be written to Parquet. The core <c>Reader</c> drives it column by column, which lets
/// the whole existing decode path be reused without exposing any internal per-type readers.
/// </summary>
/// <remarks>
/// Values arrive one column at a time: <see cref="BeginColumn"/> allocates and selects the active column buffer, then
/// <c>Hydrate</c> is called once per row. The <see cref="Row"/> result type is a sentinel - conversion collects data
/// as a side effect, so <see cref="GetResult"/> returns an empty array.
/// </remarks>
internal sealed class ParquetColumnSink : IMaterializer<ParquetColumnSink.Row>
{
    /// <summary>Sentinel row type; never materialized. The sink collects columns, not rows.</summary>
    internal sealed class Row;

    private static readonly Row[] EmptyResult = [];

    private readonly ColumnValueType[] kinds;
    private readonly Array[] buffers;
    private int currentColumn = -1;
    private int currentIndex;

    /// <summary>Number of rows seen; every column in a well-formed file shares the same count.</summary>
    public int RowCount { get; private set; }

    public ParquetColumnSink(ImmutableArray<ColumnInfo> columns)
    {
        kinds = new ColumnValueType[columns.Length];
        buffers = new Array[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            kinds[i] = columns[i].ValueType;
        }
    }

    public bool BeginColumn(int column, int rowCount)
    {
        currentColumn = column;
        currentIndex = 0;
        RowCount = rowCount;
        buffers[column] = AllocateBuffer(kinds[column], rowCount);
        return true;
    }

    public void Hydrate<TPropValue>(TPropValue value)
        where TPropValue : struct
    {
        var i = currentIndex++;
        var buffer = buffers[currentColumn];
        if (typeof(TPropValue) == typeof(DateTime))
        {
            ((DateTime[])buffer)[i] = Unsafe.As<TPropValue, DateTime>(ref value);
        }
        else if (typeof(TPropValue) == typeof(TimeSpan))
        {
            ((long[])buffer)[i] = Unsafe.As<TPropValue, TimeSpan>(ref value).Ticks;
        }
        else if (typeof(TPropValue) == typeof(float))
        {
            ((float[])buffer)[i] = Unsafe.As<TPropValue, float>(ref value);
        }
        else if (typeof(TPropValue) == typeof(double))
        {
            ((double[])buffer)[i] = Unsafe.As<TPropValue, double>(ref value);
        }
        else if (typeof(TPropValue) == typeof(decimal))
        {
            ((decimal[])buffer)[i] = Unsafe.As<TPropValue, decimal>(ref value);
        }
        else if (typeof(TPropValue) == typeof(int))
        {
            ((int[])buffer)[i] = Unsafe.As<TPropValue, int>(ref value);
        }
        else if (typeof(TPropValue) == typeof(long))
        {
            ((long[])buffer)[i] = Unsafe.As<TPropValue, long>(ref value);
        }
        else if (typeof(TPropValue) == typeof(bool))
        {
            ((bool[])buffer)[i] = Unsafe.As<TPropValue, bool>(ref value);
        }
        else
        {
            throw new NotSupportedException($"Unsupported value type '{typeof(TPropValue)}'.");
        }
    }

    public void Hydrate(string value)
        => ((string[])buffers[currentColumn])[currentIndex++] = value;

    public Row[] GetResult() => EmptyResult;

    /// <summary>Writes the <paramref name="length"/> rows starting at <paramref name="start"/> of one column to a row group.</summary>
    public async Task WriteColumnAsync(ParquetRowGroupWriter rowGroup, int column, DataField field, int start, int length)
    {
        var buffer = buffers[column];
        switch (kinds[column])
        {
            // Value-type columns bind to WriteAsync<T>(field, ReadOnlyMemory<T>); Memory<T> converts implicitly.
            case ColumnValueType.DateTimeOrdered:
            case ColumnValueType.DateTimeUnordered:
                await rowGroup.WriteAsync<DateTime>(field, ((DateTime[])buffer).AsMemory(start, length));
                break;

            case ColumnValueType.TimeSpan:
            case ColumnValueType.Int64:
            case ColumnValueType.Int64Raw:
                await rowGroup.WriteAsync<long>(field, ((long[])buffer).AsMemory(start, length));
                break;

            case ColumnValueType.Float:
            case ColumnValueType.FloatRaw:
            case ColumnValueType.ScaledNumber32:
                await rowGroup.WriteAsync<float>(field, ((float[])buffer).AsMemory(start, length));
                break;

            case ColumnValueType.Double:
            case ColumnValueType.DoubleRaw:
            case ColumnValueType.ScaledNumber64:
                await rowGroup.WriteAsync<double>(field, ((double[])buffer).AsMemory(start, length));
                break;

            case ColumnValueType.Decimal:
                await rowGroup.WriteAsync<decimal>(field, ((decimal[])buffer).AsMemory(start, length));
                break;

            case ColumnValueType.Int32:
            case ColumnValueType.Int32Raw:
                await rowGroup.WriteAsync<int>(field, ((int[])buffer).AsMemory(start, length));
                break;

            case ColumnValueType.Bool:
                await rowGroup.WriteAsync<bool>(field, ((bool[])buffer).AsMemory(start, length));
                break;

            // String (category) columns bind to the non-generic WriteAsync(field, IReadOnlyCollection<string>).
            case ColumnValueType.Category:
                await rowGroup.WriteAsync(field, new ArraySegment<string>((string[])buffer, start, length));
                break;

            default:
                throw new NotSupportedException($"Column type '{kinds[column]}' cannot be converted to Parquet.");
        }
    }

    private static Array AllocateBuffer(ColumnValueType kind, int rowCount) => kind switch
    {
        ColumnValueType.DateTimeOrdered or ColumnValueType.DateTimeUnordered => new DateTime[rowCount],
        ColumnValueType.TimeSpan or ColumnValueType.Int64 or ColumnValueType.Int64Raw => new long[rowCount],
        ColumnValueType.Float or ColumnValueType.FloatRaw or ColumnValueType.ScaledNumber32 => new float[rowCount],
        ColumnValueType.Double or ColumnValueType.DoubleRaw or ColumnValueType.ScaledNumber64 => new double[rowCount],
        ColumnValueType.Decimal => new decimal[rowCount],
        ColumnValueType.Int32 or ColumnValueType.Int32Raw => new int[rowCount],
        ColumnValueType.Bool => new bool[rowCount],
        ColumnValueType.Category => new string[rowCount],
        _ => throw new NotSupportedException($"Column type '{kind}' cannot be converted to Parquet."),
    };
}
