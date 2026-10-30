namespace Easy.TimeSeries.Aggregation;

/// <summary>A write-time aggregation problem, reported through <see cref="AggregationConfiguration.OnFailure"/>.</summary>
/// <param name="Kind">What failed.</param>
/// <param name="Spec">The aggregation concerned.</param>
/// <param name="SubjectId">Subject of the raw and aggregated files.</param>
/// <param name="StorageSpanStart">Start of the aggregated storage time span.</param>
/// <param name="RawPath">The raw file written, for failures in the write path.</param>
/// <param name="Attempt">Failed attempts so far, for marker processing.</param>
/// <param name="Exception">The cause, when there is one.</param>
public sealed record AggregationFailure(
    AggregationFailureKind Kind,
    AggregationSpec Spec,
    string SubjectId,
    DateTime StorageSpanStart,
    string? RawPath,
    int Attempt,
    Exception? Exception);

/// <summary>Kinds of <see cref="AggregationFailure"/>.</summary>
public enum AggregationFailureKind
{
    /// <summary>Immediate aggregation of an equal storage time span failed; a marker was set.</summary>
    ImmediateFailed = 1,

    /// <summary>Setting a marker after a raw write failed; recovery will find the stale file.</summary>
    MarkerWriteFailed,

    /// <summary>One marker processing attempt failed; it will be retried.</summary>
    AttemptFailed,

    /// <summary>The last allowed attempt failed; the marker moved to the failed folder.</summary>
    MovedToFailed,

    /// <summary>A marker or stale file older than the raw retention was rejected.</summary>
    RejectedByRetention,
}
