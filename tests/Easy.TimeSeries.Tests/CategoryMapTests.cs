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

    /// <summary>
    /// Regression test: the map length is a ushort, so 20,000 labels of 6 characters (140 kB) wrapped it and the
    /// writer produced a file the reader rejected.
    /// </summary>
    [Fact]
    public void Map_too_large_to_serialize_is_rejected()
    {
        var keys = Enumerable.Range(0, 20_000).Select(i => $"s{i:D5}").ToArray();
        var ex = Assert.Throws<ArgumentException>(() => new CategoryMap(keys));
        Assert.Contains("65535", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Plzeň")]
    [InlineData("Brno-ž")]
    [InlineData("€")]
    public void Non_ascii_labels_are_rejected(string label)
    {
        var ex = Assert.Throws<ArgumentException>(() => new CategoryMap(["ok", label]));
        Assert.Contains(label, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Writer_rejects_non_ascii_labels_instead_of_corrupting_them()
    {
        Assert.Throws<ArgumentException>(() => new Writer(2).AddCategory(["Brno-ž", "Brno-š"], "city"));
    }

    [Fact]
    public void Largest_serializable_map_round_trips()
    {
        // 2 bytes count + 16,383 x (1 length byte + 3 characters) = 65,534 bytes.
        var keys = Enumerable.Range(0, 16_383).Select(i => $"{(char)('a' + (i / 676))}{(char)('a' + (i / 26 % 26))}{(char)('a' + (i % 26))}").ToArray();
        var sut = new CategoryMap(keys);
        var buffer = new byte[sut.SizeHint + 2];
        sut.WriteTo(buffer);

        Assert.True(CategoryMap.TryReadFrom(buffer, out var actual, out _));
        Assert.Equal(keys.Length, actual.Count);
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
