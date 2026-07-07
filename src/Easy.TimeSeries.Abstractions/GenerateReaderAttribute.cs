namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Apply to a DTO to have the source generator emit a strongly-typed reader and materializer for it, avoiding the
/// reflection-based fallback. Pair with <see cref="GenerateWriterAttribute"/> for a full round trip.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public class GenerateReaderAttribute : Attribute;
