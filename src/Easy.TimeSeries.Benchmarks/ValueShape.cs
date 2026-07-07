namespace Easy.TimeSeries.Benchmarks;

/// <summary>
/// Data shapes shared by the XOR + block encoder benchmarks (<see cref="Int32Benchmark"/>,
/// <see cref="Int64Benchmark"/>). Encode/decode cost is entirely data-dependent - the number of
/// leading/trailing zeros in consecutive XORs decides which branch of the state machine runs -
/// so each shape targets one branch on purpose.
/// </summary>
public enum ValueShape
{
    /// <summary>Every value identical: exercises the 1-bit "same value" fast path.</summary>
    Constant,

    /// <summary>Small cumulative steps: XOR stays inside the previous block, so the reuse-block path dominates.</summary>
    Drift,

    /// <summary>Widely varying magnitudes (including negatives): forces a fresh block descriptor on most records.</summary>
    Jumpy,
}
