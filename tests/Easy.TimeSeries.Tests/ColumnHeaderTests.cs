namespace Easy.TimeSeries.Tests;

public class ColumnHeaderTests
{
    [Theory]
    [InlineData(1UL, 7, 63)]
    [InlineData(uint.MaxValue, 1200, 0)]
    [InlineData(1034, 2900, 7)]
    public void WriteAndReadFromSpan(long dataLength, int records, int bitsInLastWord)
    {
        var buffer = new byte[ColumnHeader.Size].AsSpan();
        var expected = new ColumnHeader(dataLength, records, bitsInLastWord);
        expected.WriteTo(buffer);
        var ok = ColumnHeader.TryReadFrom(buffer, out var actual);
        Assert.True(ok);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void ThrowsOnIvalidDataLength(long dataLength)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new ColumnHeader(dataLength, 100, 12);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ThrowsOnIvalidRecords(int records)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new ColumnHeader(9500, records, 12);
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ThrowsOnInvalidBitsInLastWord(int bitsInLastWord)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new ColumnHeader(1000, 100, bitsInLastWord);
        });
    }
}
