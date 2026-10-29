namespace Easy.TimeSeries.Aggregation;

/// <summary>
/// Computes one aggregated column from the raw rows of an aggregation slice. Predefined and custom functions share
/// this contract; the spec holds them through this non-generic base.
/// </summary>
public interface IAggregationFunction
{
    /// <summary>Aggregated column label.</summary>
    string Name { get; }

    /// <summary>Raw columns read by the function; their order defines the index used with the slice.</summary>
    IReadOnlyList<AggregationInput> Inputs { get; }

    /// <summary>Encoding of the aggregated column. Inherited encodings are valid only for a single input.</summary>
    OutputEncoding Output { get; }

    /// <summary>CLR type of the result; one of the nine decoded column types.</summary>
    Type ResultType { get; }
}

/// <summary>Typed contract of an aggregation function.</summary>
/// <typeparam name="TOut">Result type; one of the nine decoded column types.</typeparam>
public interface IAggregationFunction<TOut> : IAggregationFunction
{
    Type IAggregationFunction.ResultType => typeof(TOut);

    /// <summary>Called once per aggregation slice. Must be thread-safe; the same instance serves parallel runs.</summary>
    TOut Compute(in AggregationSlice slice);
}
