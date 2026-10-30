namespace Easy.TimeSeries.Storage;

/// <summary>
/// Serializes the commits and deletes of one local file within the process, so a version check and the rename or
/// delete that follows it cannot interleave with another writer of the same file.
/// </summary>
internal static class PathLocks
{
    private static readonly Lock[] Stripes = [.. Enumerable.Range(0, 64).Select(_ => new Lock())];

    /// <remarks>Case-insensitive, so one file on a case-insensitive file system always maps to one stripe.</remarks>
    public static Lock For(string fullPath)
        => Stripes[(StringComparer.OrdinalIgnoreCase.GetHashCode(fullPath) & int.MaxValue) % Stripes.Length];
}
