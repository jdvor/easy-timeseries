namespace Easy.TimeSeries.SrcGen;

/// <summary>
/// Metadata names of types the generator recognizes. The generator must not reference
/// Easy.TimeSeries.Abstractions (or the core library) at runtime, so all matching is done
/// by fully qualified metadata name strings.
/// </summary>
internal static class KnownNames
{
    public const string ColumnAttribute = "Easy.TimeSeries.Abstractions.ColumnAttribute";
    public const string GenerateWriterAttribute = "Easy.TimeSeries.Abstractions.GenerateWriterAttribute";
    public const string GenerateReaderAttribute = "Easy.TimeSeries.Abstractions.GenerateReaderAttribute";

    /// <summary>Presence of this type signals that the compilation references the core library.</summary>
    public const string CoreWriterType = "Easy.TimeSeries.Writer";

    public const string TimeSpanType = "System.TimeSpan";

    /// <summary>Named arguments of <c>ColumnAttribute</c>.</summary>
    public static class ColumnArgs
    {
        public const string ValueType = "ValueType";
        public const string Label = "Label";
        public const string DateTimePrecision = "DateTimePrecision";
        public const string DateTimeSort = "DateTimeSort";
        public const string TimeSpanPrecision = "TimeSpanPrecision";
        public const string NumberPrecision = "NumberPrecision";
        public const string NumberDistribution = "NumberDistribution";
    }
}
