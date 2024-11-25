namespace Easy.TimeSeries.Tests;

public class IdAccumulatorTests
{
    [Fact]
    public void Accumulates()
    {
        var acc = new IdAccumulator(TestData1.Length);
        acc.AddRange(TestData1);
        var (keys, iterator) = acc.BuildIterator();
        var ids = iterator.ToArray();
        Assert.True(IsSame(ExpectedKeys, keys, out var msg), msg);
        Assert.True(IsSame(ExpectedIds, ids, out msg), msg);
    }

    [Fact]
    public void AccumulatesEvenDifferencesInCase()
    {
        var acc = new IdAccumulator(TestData2.Length, StringComparer.OrdinalIgnoreCase);
        acc.AddRange(TestData2);
        var (keys, iterator) = acc.BuildIterator();
        var ids = iterator.ToArray();
        Assert.True(IsSame(ExpectedKeys, keys, out var msg), msg);
        Assert.True(IsSame(ExpectedIds, ids, out msg), msg);
    }

    // 2x A, 3x B, 4x C
    private static readonly string[] TestData1 = ["A", "B", "C", "A", "B", "C", "B", "C", "C"];

    // same as above with values of varying case
    private static readonly string[] TestData2 = ["A", "B", "C", "a", "b", "c", "b", "c", "c"];

    private static readonly short[] ExpectedIds = [2, 1, 0, 2, 1, 0, 1, 0, 0];
    private static readonly string[] ExpectedKeys = ["C", "B", "A"];

    private static bool IsSame<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, out string message)
        where T : IEquatable<T>
    {
        if (expected.Count != actual.Count)
        {
            message = "array lengths are not same";
            return false;
        }

        for (var i = 0; i < expected.Count; i++)
        {
            if (!expected[i].Equals(actual[i]))
            {
                message = $"array elements differ at index {i}";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }
}
