namespace Easy.TimeSeries.Tests;

using Easy.TimeSeries.Abstractions;

/// <summary>
/// Single source of truth for the on-disk format fixtures under <c>tests/data/golden/v1</c>.
/// Each fixture pairs the exact input used to produce a committed <c>.ets</c> file with the values a reader must
/// decode back out of it. See <see cref="GoldenFileTests"/> for how both halves are asserted, and
/// <c>docs/layout.md</c> for the format itself.
/// </summary>
/// <remarks>
/// Values are fixed literals on purpose - nothing here may depend on the clock, the culture, or the machine, or
/// the committed bytes would not be reproducible.
///
/// The Brotli-backed "random distribution" columns (<c>Int32Raw</c>, <c>Int64Raw</c>, <c>FloatRaw</c>,
/// <c>DoubleRaw</c>) are deliberately absent: their payload is whatever the runtime's Brotli encoder produces, so
/// byte-exact expectations would break on a .NET upgrade rather than on a real format change.
/// </remarks>
internal static class GoldenFixtures
{
    /// <summary>Format version the fixtures were produced with; also the directory name under <c>golden/</c>.</summary>
    public const string FormatVersion = "v1";

    private static readonly DateTime Epoch2026 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static int[] Int32Values { get; } =
        [0, 1, -1, 42, 43, 44, 1000, 999, -2_147_483_648, 2_147_483_647, 7, 7];

    private static long[] Int64Values { get; } =
        [0L, 1L, -1L, 5_000_000_000L, 5_000_000_001L, -9_223_372_036_854_775_808L, 9_223_372_036_854_775_807L,
         100L, 100L, 100L, -50L, 0L];

    private static float[] FloatValues { get; } =
        [0f, 1.5f, 1.5f, -2.25f, 3.125f, 100.5f, 100.5f, 0.0001f, -0.0001f, 12345.678f, 1f, 0f];

    private static double[] DoubleValues { get; } =
        [0d, 1.5d, 1.5d, -2.25d, 3.125d, 100.5d, 100.5d, 0.0001d, -0.0001d, 12345.6789d, 1d, 0d];

    private static bool[] BoolValues { get; } =
        [true, false, true, true, false, false, true, false, true, true, true, false];

    private static decimal[] DecimalValues { get; } =
        [0m, 1.5m, -2.25m, 3.125m, 100.50m, 0.0001m, -0.0001m, 12345.6789m, 999999.99m, 1m, 1m, 0m];

    private static string[] CategoryValues { get; } =
        ["ok", "degraded", "ok", "ok", "failed", "degraded", "ok", "failed", "ok", "ok", "degraded", "ok"];

    private static TimeSpan[] TimeSpanValues { get; } =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromDays(1),
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromMilliseconds(-1),
        TimeSpan.FromHours(-2),
        TimeSpan.FromMinutes(90),
        TimeSpan.FromMinutes(90),
        TimeSpan.Zero,
    ];

    private static float[] ScaledFloatValues { get; } =
        [0f, 1.5f, 1.5f, -2.25f, 3.13f, 100.5f, 100.5f, 0.01f, -0.01f, 12345.67f, 1f, 0f];

    private static double[] ScaledDoubleValues { get; } =
        [0d, 1.5d, 1.5d, -2.25d, 3.13d, 100.5d, 100.5d, 0.01d, -0.01d, 12345.67d, 1d, 0d];

    /// <summary>
    /// Regular 15-minute sampling with two repeats and one gap - the delta-of-delta happy path plus edges.
    /// </summary>
    /// <remarks>
    /// Gaps are kept under ~24 days on purpose. At millisecond precision the widest delta-of-delta bucket is 32
    /// bits, so a delta-of-delta beyond +/-2^31 ms (~24.86 days) is silently truncated by the ordered writer. That
    /// is a live defect, not a property of the format worth pinning here - see the note in artifacts/fix1.md.
    /// </remarks>
    private static DateTime[] OrderedTimestamps { get; } =
    [
        Epoch2026,
        Epoch2026.AddMinutes(15),
        Epoch2026.AddMinutes(30),
        Epoch2026.AddMinutes(45),
        Epoch2026.AddMinutes(60),
        Epoch2026.AddMinutes(60),
        Epoch2026.AddMinutes(75),
        Epoch2026.AddMinutes(90),
        Epoch2026.AddHours(4),
        Epoch2026.AddHours(4).AddMilliseconds(250),
        Epoch2026.AddDays(1),
        Epoch2026.AddDays(2),
    ];

    /// <summary>Out of order, with repeats and a pre-2000 value that the ordered encoder cannot represent.</summary>
    private static DateTime[] UnorderedTimestamps { get; } =
    [
        Epoch2026.AddHours(5),
        Epoch2026,
        Epoch2026.AddDays(-1),
        new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(1999, 12, 31, 23, 59, 59, DateTimeKind.Utc),
        Epoch2026.AddMinutes(15),
        Epoch2026.AddMinutes(15),
        Epoch2026.AddDays(400),
        Epoch2026.AddMilliseconds(1),
        new DateTime(2100, 6, 15, 12, 0, 0, DateTimeKind.Utc),
        Epoch2026,
        Epoch2026.AddSeconds(30),
    ];

    public static IReadOnlyList<GoldenFixture> All { get; } =
    [
        new GoldenFixture(
            "int32",
            () => new Writer(Int32Values.Length).AddInt32(Int32Values, "i32"),
            [[.. Int32Values.Cast<object>()]]),

        new GoldenFixture(
            "int64",
            () => new Writer(Int64Values.Length).AddInt64(Int64Values, "i64"),
            [[.. Int64Values.Cast<object>()]]),

        new GoldenFixture(
            "float",
            () => new Writer(FloatValues.Length).AddFloat(FloatValues, "f32"),
            [[.. FloatValues.Cast<object>()]]),

        new GoldenFixture(
            "double",
            () => new Writer(DoubleValues.Length).AddDouble(DoubleValues, "f64"),
            [[.. DoubleValues.Cast<object>()]]),

        new GoldenFixture(
            "bool",
            () => new Writer(BoolValues.Length).AddBool(BoolValues, "flag"),
            [[.. BoolValues.Cast<object>()]]),

        new GoldenFixture(
            "decimal",
            () => new Writer(DecimalValues.Length).AddDecimal(DecimalValues, "dec"),
            [[.. DecimalValues.Cast<object>()]]),

        new GoldenFixture(
            "category",
            () => new Writer(CategoryValues.Length).AddCategory(CategoryValues, "cat"),
            [[.. CategoryValues.Cast<object>()]]),

        new GoldenFixture(
            "timespan",
            () => new Writer(TimeSpanValues.Length).AddInterval(TimeSpanValues, "span"),
            [[.. TimeSpanValues.Cast<object>()]]),

        // decimalPlaces: 2 - the scale is stored in ColumnInfo.Meta as the number of places, not as 10^places.
        new GoldenFixture(
            "scalednumber32",
            () => new Writer(ScaledFloatValues.Length).AddScaledNumber32(ScaledFloatValues, "sn32", 2),
            [[.. ScaledFloatValues.Cast<object>()]]),

        new GoldenFixture(
            "scalednumber64",
            () => new Writer(ScaledDoubleValues.Length).AddScaledNumber64(ScaledDoubleValues, "sn64", 2),
            [[.. ScaledDoubleValues.Cast<object>()]]),

        new GoldenFixture(
            "datetime-ordered",
            () => new Writer(OrderedTimestamps.Length).AddTimeOrdered(OrderedTimestamps, "ts"),
            [[.. OrderedTimestamps.Cast<object>()]]),

        new GoldenFixture(
            "datetime-unordered",
            () => new Writer(UnorderedTimestamps.Length).AddTimeUnordered(UnorderedTimestamps, "ts"),
            [[.. UnorderedTimestamps.Cast<object>()]]),

        // Exercises multi-column layout: column ordering, per-column headers, and the header column table.
        new GoldenFixture(
            "mixed",
            () => new Writer(OrderedTimestamps.Length)
                .AddTimeOrdered(OrderedTimestamps, "ts")
                .AddDouble(DoubleValues, "value")
                .AddInt32(Int32Values, "quality")
                .AddCategory(CategoryValues, "status")
                .AddBool(BoolValues, "valid"),
            [
                [.. OrderedTimestamps.Cast<object>()],
                [.. DoubleValues.Cast<object>()],
                [.. Int32Values.Cast<object>()],
                [.. CategoryValues.Cast<object>()],
                [.. BoolValues.Cast<object>()],
            ]),
    ];
}

/// <summary>One golden file: how to produce it, and what a reader must get back out of it.</summary>
/// <param name="Name">File name without extension, under <c>tests/data/golden/{version}</c>.</param>
/// <param name="Build">Builds a writer over the fixture's input. Called fresh per assertion.</param>
/// <param name="Expected">Expected decoded values, outer index is the column index.</param>
internal sealed record GoldenFixture(string Name, Func<Writer> Build, object[][] Expected);

/// <summary>
/// Collects decoded values per column instead of hydrating a DTO, so one implementation covers every fixture
/// regardless of its column shape.
/// </summary>
internal sealed class CapturingMaterializer : IMaterializer<CapturingMaterializer.Row>
{
    private readonly Dictionary<int, List<object>> columns = [];
    private List<object>? current;

    /// <summary>Decoded values keyed by column index, in read order.</summary>
    public IReadOnlyDictionary<int, List<object>> Columns => columns;

    public bool BeginColumn(int column, int rowCount)
    {
        current = new List<object>(rowCount);
        columns[column] = current;
        return true;
    }

    public void Hydrate<TPropValue>(TPropValue value)
        where TPropValue : struct
        => current!.Add(value);

    public void Hydrate(string value) => current!.Add(value);

    public Row[] GetResult() => [];

    /// <summary>Unused - the reader's generic constraint requires a row type, but nothing is materialized.</summary>
    internal sealed class Row;
}
