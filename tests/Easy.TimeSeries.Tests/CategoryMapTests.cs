namespace Easy.TimeSeries.Tests;

public class CategoryMapTests
{
    [Theory]
    [MemberData(nameof(BasicAsPairOfLabelAndId))]
    public void TranslateLabelToId(string label, short expected)
    {
        var sut = new CategoryMap(Basic);
        var actual = sut.GetId(label);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(BasicAsPairOfIdAndLabel))]
    public void TranslateIdToLabel(short id, string expected)
    {
        var sut = new CategoryMap(Basic);
        var actual = sut.GetLabel(id);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WriteAndReadFromSpan()
    {
        var sut = new CategoryMap(Basic);
        var buffer = new byte[sut.SizeHint].AsSpan();
        var written = sut.WriteTo(buffer);
        var ok = CategoryMap.TryReadFrom(buffer, out var actual, out var bytesRead);

        Assert.True(ok, "failed to read back");
        Assert.True(written == bytesRead, "written != bytesRead");
        Assert.True(Basic.Count == actual.Count, "Basic.Count != actual.Count");
    }

    [Fact]
    public void ThrowsOnIvalidMapData()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new CategoryMap(Invalid);
        });
    }

    [Fact]
    public void ThrowsOnUnmappedLabel()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var sut = new CategoryMap(Basic);
            _ = sut.GetId("does not exist");
        });
    }

    [Fact]
    public void ThrowsOnUnmappedId()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var sut = new CategoryMap(Basic);
            _ = sut.GetLabel(10);
        });
    }

    public static IEnumerable<object[]> BasicAsPairOfLabelAndId()
        => Basic.Select(x => new object[] { x.Key, x.Value });

    public static IEnumerable<object[]> BasicAsPairOfIdAndLabel()
        => Basic.Select(x => new object[] { x.Value, x.Key });

    private static readonly Dictionary<string, short> Basic = new()
    {
        { "None", 0 },
        { "Apple", 1 },
        { "Orange", 2 },
        { "Banana", 3 },
        { "Passion fruit", 4 },
        { "Pineapple", 5 },
    };

    // Ids are not continuous.
    private static readonly Dictionary<string, short> Invalid = new()
    {
        { "None", 0 },
        { "Apple", 2 },
        { "Orange", 3 },
        { "Banana", 4 },
        { "Passion fruit", 5 },
        { "Pineapple", 6 },
    };
}
