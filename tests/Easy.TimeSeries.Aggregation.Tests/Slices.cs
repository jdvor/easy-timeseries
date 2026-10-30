namespace Easy.TimeSeries.Aggregation.Tests;

/// <summary>Builds slices over hand-written column arrays, for testing functions without the engine.</summary>
internal static class Slices
{
    public static readonly DateTime Start = new(2026, 10, 25, 14, 5, 0, DateTimeKind.Utc);

    /// <summary>A slice over all rows of the given input columns, one input per array, timestamps one second apart.</summary>
    public static T Over<T>(Func<AggregationSlice, T> compute, params Array[] inputs)
    {
        var count = inputs[0].Length;
        var data = new SliceData([.. inputs.Select(x => x.GetType().GetElementType()!)])
        {
            Times = [.. Enumerable.Range(0, count).Select(i => Start.AddSeconds(i))],
        };

        for (var i = 0; i < inputs.Length; i++)
        {
            data.Columns[i] = inputs[i];
        }

        using (data)
        {
            data.NextSlice();
            var slice = new AggregationSlice(
                data,
                [.. Enumerable.Range(0, inputs.Length)],
                0,
                count,
                Start,
                Start.AddMinutes(1),
                null);
            return compute(slice);
        }
    }

    /// <summary>Computes <paramref name="function"/> over one slice of the given input columns.</summary>
    public static TOut Compute<TOut>(IAggregationFunction<TOut> function, params Array[] inputs)
        => Over(s => function.Compute(s), inputs);
}
