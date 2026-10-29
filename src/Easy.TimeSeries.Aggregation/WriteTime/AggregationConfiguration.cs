namespace Easy.TimeSeries.Aggregation;

using Easy.TimeSeries.Aggregation.WriteTime;
using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;

/// <summary>
/// Write-time aggregation setup shared by <see cref="AggregatingCatalog"/> and <see cref="AggregationMaintenance"/>:
/// the raw layout, the aggregations computed from it, and how dirty markers are processed.
/// </summary>
public sealed record AggregationConfiguration
{
    /// <summary>Layout of the raw files: root and raw storage time span.</summary>
    public required PathBuilder Raw { get; init; }

    /// <summary>Aggregations computed from the raw files; each needs its own root.</summary>
    public required IReadOnlyList<AggregationSpec> Specs { get; init; }

    /// <summary>
    /// How long raw files are kept. A span starting earlier than this is never recomputed, because some of its raw
    /// files may be gone. Must be at least the longest storage time span plus <see cref="GracePeriod"/>, so a span can
    /// still be computed when it closes. <c>Timeout.InfiniteTimeSpan</c> keeps raw forever.
    /// </summary>
    public required TimeSpan RawRetention { get; init; }

    /// <summary>Root of the dirty markers; must not overlap the raw or any aggregated root.</summary>
    public string MarkerRoot { get; init; } = "dirty";

    /// <summary>How long after a storage time span ends its markers are still left alone, for late raw writes.</summary>
    public TimeSpan GracePeriod { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Failed attempts after which a marker moves to the failed folder.</summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Wait before retrying a failed marker; it doubles with each further failure, so a short storage outage does not
    /// use up <see cref="MaxAttempts"/>. With the defaults, a marker moves to the failed folder 30 minutes after its
    /// first failure at the earliest.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Age after which recovery deletes a temp file of a <see cref="FileCatalog"/> as left by a crashed write. Must
    /// exceed the longest write.
    /// </summary>
    public TimeSpan OrphanedTempFileAge { get; init; } = TimeSpan.FromHours(1);

    /// <summary>How many aggregated files one <see cref="AggregationMaintenance.ProcessDirtyAsync"/> call computes at once.</summary>
    public int MaxDegreeOfParallelism { get; init; } = 1;

    /// <summary>Clock deciding when spans are closed and how old they are.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>Monitoring only. Exceptions thrown by the callback are caught and ignored.</summary>
    public Action<AggregationFailure>? OnFailure { get; init; }

    /// <summary>Whether raw files of a span starting at <paramref name="spanStart"/> may already be deleted.</summary>
    internal bool IsBeyondRetention(DateTime spanStart)
        => RawRetention != Timeout.InfiniteTimeSpan
           && spanStart < TimeProvider.GetUtcNow().UtcDateTime - RawRetention;

    /// <summary>Wait after the given number of failed attempts; the doubling is capped at 1024 times the retry delay.</summary>
    internal TimeSpan RetryDelayAfter(int attempts)
        => RetryDelay * Math.Pow(2, Math.Clamp(attempts - 1, 0, 10));

    /// <summary>Reports a failure; the callback can never break the caller.</summary>
    internal void Report(AggregationFailure failure)
    {
        try
        {
            OnFailure?.Invoke(failure);
        }
#pragma warning disable CA1031 // monitoring callback must not affect aggregation
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <exception cref="ArgumentException">The configuration is inconsistent.</exception>
    internal AggregationConfiguration Validate()
    {
        ArgumentNullException.ThrowIfNull(Raw, nameof(Raw));
        ArgumentNullException.ThrowIfNull(Specs, nameof(Specs));
        ArgumentNullException.ThrowIfNull(TimeProvider, nameof(TimeProvider));
        StoragePath.Validate(MarkerRoot, paramName: nameof(MarkerRoot));
        if (RawRetention <= TimeSpan.Zero && RawRetention != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentException("Raw retention must be positive or Timeout.InfiniteTimeSpan.", nameof(RawRetention));
        }

        if (GracePeriod < TimeSpan.Zero || OrphanedTempFileAge < TimeSpan.Zero || RetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentException("Grace period, retry delay and orphaned temp file age must not be negative.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(MaxAttempts, 1, nameof(MaxAttempts));
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDegreeOfParallelism, 1, nameof(MaxDegreeOfParallelism));

        var roots = new List<(string Root, string What)> { (MarkerRoot, "marker root") };
        if (Raw.Root.Length > 0)
        {
            roots.Add((Raw.Root, "raw root"));
        }

        foreach (var spec in Specs)
        {
            ArgumentNullException.ThrowIfNull(spec, nameof(Specs));
            if (spec.StorageTimeSpan < Raw.Granularity)
            {
                throw new ArgumentException(
                    $"Spec '{spec.Root}' stores {spec.StorageTimeSpan} files, shorter than the raw {Raw.Granularity} files.",
                    nameof(Specs));
            }

            var needed = MaxLength(spec.StorageTimeSpan) + GracePeriod;
            if (RawRetention != Timeout.InfiniteTimeSpan && RawRetention < needed)
            {
                throw new ArgumentException(
                    $"Raw retention {RawRetention} is shorter than the {spec.StorageTimeSpan} storage time span of spec "
                    + $"'{spec.Root}' plus the grace period ({needed}); its first raw files would be gone before it closes.",
                    nameof(RawRetention));
            }

            // Inside the marker root, failed markers live under {marker root}/failed/{spec root}; an active marker root
            // starting with that folder could be the failed marker root of another spec.
            if (string.Equals(FirstSegment(spec.Root), MarkerStore.FailedFolder, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Spec root '{spec.Root}' starts with '{MarkerStore.FailedFolder}', which is reserved for failed markers.",
                    nameof(Specs));
            }

            roots.Add((spec.Root, $"aggregated root of spec '{spec.Root}'"));
        }

        for (var i = 0; i < roots.Count; i++)
        {
            if (Raw.Root.Length == 0 && FirstSegment(roots[i].Root).All(char.IsAsciiDigit))
            {
                throw new ArgumentException(
                    $"The {roots[i].What} '{roots[i].Root}' starts like a raw date folder while the raw root is empty.");
            }

            for (var j = i + 1; j < roots.Count; j++)
            {
                if (Overlaps(roots[i].Root, roots[j].Root))
                {
                    throw new ArgumentException($"The {roots[i].What} '{roots[i].Root}' overlaps the {roots[j].What} '{roots[j].Root}'.");
                }
            }
        }

        return this;
    }

    private static TimeSpan MaxLength(TimeGranularity granularity)
        => granularity switch
        {
            TimeGranularity.Hour => TimeSpan.FromHours(1),
            TimeGranularity.Day => TimeSpan.FromDays(1),
            TimeGranularity.Month => TimeSpan.FromDays(31),
            _ => TimeSpan.FromDays(366),
        };

    private static string FirstSegment(string path) => path.Split('/')[0];

    private static bool Overlaps(string a, string b)
        => string.Equals(a, b, StringComparison.Ordinal)
           || a.StartsWith(b + "/", StringComparison.Ordinal)
           || b.StartsWith(a + "/", StringComparison.Ordinal);
}
