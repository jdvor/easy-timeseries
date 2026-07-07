namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Apply to a DTO to have the source generator emit a strongly-typed writer for it that maps each
/// <see cref="ColumnAttribute"/>-annotated property to the matching column encoder. Pair with
/// <see cref="GenerateReaderAttribute"/> for a full round trip.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public class GenerateWriterAttribute : Attribute;
