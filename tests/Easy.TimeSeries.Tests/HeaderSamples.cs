namespace Easy.TimeSeries.Tests;

using System.Collections.Immutable;

internal static class HeaderSamples
{
    internal static (Header Instance, string UseCase)[] All()
    {
        return new (Header Instance, string UseCase)[]
        {
            (OneColumn(), nameof(OneColumn)),
            (ThreeColumns(), nameof(ThreeColumns)),
            (ThreeColumnsOneWithEmptyLabel(), nameof(ThreeColumnsOneWithEmptyLabel)),
            (NonAsciiChars(), nameof(NonAsciiChars)),
        };
    }

    internal static Header OneColumn()
    {
        return new Header(
            Version.V1,
            ImmutableArray.Create(new[]
            {
                new ColumnInfo(0, ColumnValueType.DateTime, (int)TimePrecision.Seconds, "time"),
            }));
    }

    internal static Header ThreeColumns()
    {
        return new Header(
            Version.V1,
            ImmutableArray.Create(new[]
            {
                new ColumnInfo(0, ColumnValueType.Float, 0, "input (MW)"),
                new ColumnInfo(1, ColumnValueType.Float, 0, "output (MW)"),
                new ColumnInfo(2, ColumnValueType.Int32, 0, "tolerance (%)"),
            }));
    }

    internal static Header ThreeColumnsOneWithEmptyLabel()
    {
        return new Header(
            Version.V1,
            ImmutableArray.Create(new[]
            {
                new ColumnInfo(0, ColumnValueType.Double, 0, "impedance"),
                new ColumnInfo(1, ColumnValueType.Float, 0, string.Empty),
                new ColumnInfo(2, ColumnValueType.Int64, 0, "resistance"),
            }));
    }

    internal static Header NonAsciiChars()
    {
        return new Header(
            Version.V1,
            ImmutableArray.Create(new[]
            {
                new ColumnInfo(0, ColumnValueType.TimeSpan, (int)TimePrecision.TenthsOfSecond, "Žlutý kůň skákal úhorem."),
            }));
    }
}
