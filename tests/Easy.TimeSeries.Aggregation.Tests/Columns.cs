namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Storage;

/// <summary>Reads any file back as decoded values per column, keyed by label.</summary>
internal sealed class Columns : IMaterializer<Columns.Row>
{
    private readonly List<List<object>> values = [];
    private List<object>? current;

    public Header Header { get; private set; }

    public IReadOnlyList<object> this[string label]
        => values[Header.Columns.Single(x => string.Equals(x.Label, label, StringComparison.Ordinal)).Index];

    public int Rows => values.Count == 0 ? 0 : values[0].Count;

    public static async Task<Columns> ReadAsync(IReadStorage storage, CancellationToken ct)
    {
        var bytes = await storage.ReadAsync(ct);
        return Read(bytes);
    }

    public static Columns Read(ReadOnlyMemory<byte> bytes)
    {
        var result = new Columns();
        Header.TryReadFrom(bytes.Span, out var header, out _);
        result.Header = header;
        Reader.ReadFrom(bytes.Span, result);
        return result;
    }

    public static async Task<byte[]> WriteAsync(Writer writer, CancellationToken ct)
    {
        using var storage = new InMemoryStorage();
        using (writer)
        {
            await writer.WriteToAsync(storage, ct);
        }

        return storage.WrittenSpan.ToArray();
    }

    public bool BeginColumn(int column, int rowCount)
    {
        while (values.Count <= column)
        {
            values.Add([]);
        }

        current = values[column];
        return true;
    }

    public void Hydrate<TPropValue>(TPropValue value)
        where TPropValue : struct
        => current!.Add(value);

    public void Hydrate(string value) => current!.Add(value);

    public Row[] GetResult() => [];

    internal sealed class Row;
}
