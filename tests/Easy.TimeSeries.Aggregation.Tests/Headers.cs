namespace Easy.TimeSeries.Aggregation.Tests;

using System.Collections.Immutable;

/// <summary>Raw headers for binding tests: <c>("Voltage", ColumnValueType.Float, 0)</c> per column.</summary>
internal static class Headers
{
    public static Header Of(params (string Label, ColumnValueType Type, int Meta)[] columns)
        => new(
            Version.V1,
            [.. columns.Select((c, i) => new ColumnInfo(i, c.Type, c.Meta, c.Label))]);

    /// <summary>Time, Sensor (Category), Voltage (ScaledNumber32, 1 dp), Current (Double), Ok (Bool).</summary>
    public static Header Sensor()
        => Of(
            ("Timestamp", ColumnValueType.DateTimeOrdered, (int)TimePrecision.Milliseconds),
            ("Sensor", ColumnValueType.Category, 0),
            ("Voltage", ColumnValueType.ScaledNumber32, 1),
            ("Current", ColumnValueType.Double, 0),
            ("Ok", ColumnValueType.Bool, 0));
}
