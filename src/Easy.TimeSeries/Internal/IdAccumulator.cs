namespace Easy.TimeSeries;

internal sealed class IdAccumulator
{
    private readonly Dictionary<string, Id> ids;
    private readonly List<short> values;
    private short idSeq;

    public IdAccumulator(int capacity, IEqualityComparer<string>? comparer = null)
    {
        ids = new Dictionary<string, Id>(16, comparer);
        values = new List<short>(capacity);
    }

    public void AddRange(IEnumerable<string> strValues)
    {
        foreach (var strValue in strValues)
        {
            Add(strValue);
        }
    }

    public void Add(string strValue)
    {
        if (ids.TryGetValue(strValue, out var id))
        {
            id.Count++;
            values.Add(id.FirstId);
            return;
        }

        ids[strValue] = new Id(strValue, idSeq);
        values.Add(idSeq);
        idSeq++;
    }

    public (string[] keys, IEnumerable<short> substitutedValues) BuildIterator()
    {
        var oldIds = ids.Values.OrderByDescending(x => x.Count).ThenByDescending(x => x.Key).ToArray();
        var newIds = Enumerable.Range(0, oldIds.Length);
        var keys = oldIds.Select(x => x.Key).ToArray();
        var map = oldIds.Select(x => x.FirstId).Zip(newIds).ToDictionary(x => x.First, x => (short)x.Second);
        var substitutedValues = values.Select(value => map[value]);
        return (keys, substitutedValues);
    }

    private class Id
    {
        public string Key { get; }

        public short FirstId { get; }

        public int Count { get; set; } = 1;

        public Id(string key, short firstId)
        {
            Key = key;
            FirstId = firstId;
        }

        public override string ToString()
            => $"{Key}: {Count}";
    }
}
