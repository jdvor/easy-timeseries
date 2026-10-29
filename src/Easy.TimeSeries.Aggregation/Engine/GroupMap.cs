namespace Easy.TimeSeries.Aggregation.Engine;

/// <summary>
/// Group values of one aggregated file mapped to dense ids. Category ids are local to each raw file, so values are
/// mapped by content; the core category reader returns one string instance per id, so a reference check against the
/// previous value skips most lookups.
/// </summary>
internal sealed class GroupMap(string label)
{
    /// <summary>The Category column type stores ids as <see cref="short"/>.</summary>
    public const int MaxGroups = short.MaxValue;

    private readonly Dictionary<string, int> ids = new(StringComparer.Ordinal);
    private readonly List<string> values = [];
    private string? last;
    private int lastId = -1;

    public int Count => values.Count;

    public string this[int id] => values[id];

    public int GetId(string value)
    {
        if (ReferenceEquals(value, last))
        {
            return lastId;
        }

        if (!ids.TryGetValue(value, out var id))
        {
            if (values.Count == MaxGroups)
            {
                throw new AggregationException(
                    $"Group column '{label}' has more than {MaxGroups} distinct values in one aggregated file.",
                    column: label);
            }

            id = values.Count;
            ids.Add(value, id);
            values.Add(value);
        }

        last = value;
        lastId = id;
        return id;
    }
}
