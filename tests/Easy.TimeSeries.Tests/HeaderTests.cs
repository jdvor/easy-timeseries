namespace Easy.TimeSeries.Tests;

using KellermanSoftware.CompareNetObjects;
using System.Collections.Immutable;
using System.Security.Cryptography;

public class HeaderTests
{
    private readonly CompareLogic comparer = new();

    [Theory]
    [MemberData(nameof(WriteAndReadFromSpanData))]
    public void WriteAndReadFromSpan(Header expected, string useCase)
    {
        var size = expected.SizeHint;
        var buffer = new byte[size].AsSpan();

        var written = expected.WriteTo(buffer);
        var ok = Header.TryReadFrom(buffer[..written], out var actual, out _);

        Assert.True(ok, useCase);
        var result = comparer.Compare(expected, actual);
        Assert.True(result.AreEqual, $"{useCase}: {result.DifferencesString}");
    }

    public static IEnumerable<object[]> WriteAndReadFromSpanData()
        => HeaderSamples.All().Select(x => new object[] { x.Instance, x.UseCase });

    [Fact]
    public void ThrowsOnTooLongLabel()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var tooLong = new string(Enumerable.Range(0, ColumnInfo.MaxLabelLength + 1).Select(_ => 'a').ToArray());
            return new Header(
                Version.V1,
                ImmutableArray.Create(new[]
                {
                    new ColumnInfo(0, ColumnValueType.Float, 0, tooLong),
                }));
        });
    }

    [Fact]
    public void FailsGracefullyOnNonHeaderData()
    {
        var buffer = RandomNumberGenerator.GetBytes(50);
        var ok = Header.TryReadFrom(buffer, out var actual, out _);

        Assert.False(ok);
        Assert.True(actual.IsEmpty);
    }

    [Fact]
    public void FailsGracefullyOnTooShortBuffer()
    {
        var buffer = new byte[] { Header.H1, Header.H2, (byte)Version.V1, 0xFF };
        var ok = Header.TryReadFrom(buffer, out var actual, out _);

        Assert.False(ok);
        Assert.True(actual.IsEmpty);
    }
}
