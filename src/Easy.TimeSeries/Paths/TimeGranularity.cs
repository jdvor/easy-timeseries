namespace Easy.TimeSeries.Paths;

/// <summary>
/// Size of the time bucket a <see cref="PathBuilder"/> partitions files by. Determines the date segments in a stored
/// path (e.g. <c>yyyy/MM/dd</c> for <see cref="Day"/>) and how a time range is split into files.
/// </summary>
public enum TimeGranularity
{
    /// <summary>Unset / invalid.</summary>
    None = 0,

    /// <summary>One file per hour (<c>yyyy/MM/dd/HH</c>).</summary>
    Hour,

    /// <summary>One file per day (<c>yyyy/MM/dd</c>).</summary>
    Day,

    /// <summary>One file per month (<c>yyyy/MM</c>).</summary>
    Month,

    /// <summary>One file per year (<c>yyyy</c>).</summary>
    Year,
}
