namespace Easy.TimeSeries.Aggregation.WriteTime;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Paths;

/// <summary>
/// Raw files of one storage time span, listed once and shared by all markers of that span in one
/// <see cref="AggregationMaintenance.ProcessDirtyAsync"/> run. Without it, every marker would list the raw files of
/// all subjects of the span, because subject ids are file names.
/// </summary>
/// <remarks>
/// The listing starts on first use, so markers skipped as open never list anything, and it is dropped after the
/// span's last marker, so a run holds only the listings still in use. It is taken after the markers were listed:
/// a raw file it misses was written after its marker's version was read, which keeps that marker for the next run.
/// </remarks>
internal sealed class SpanListing(
    IStorageCatalog catalog,
    PathBuilder raw,
    DateTime spanStart,
    DateTime spanEnd,
    IReadOnlySet<string> subjects,
    int markers,
    CancellationToken cancellationToken)
{
    private readonly Lock gate = new();
    private Task<Dictionary<string, List<ListedRawFile>>>? listing;
    private int remaining = markers;

    /// <summary>Raw files of one subject in the span, possibly none.</summary>
    public async Task<IReadOnlyList<ListedRawFile>> FilesOfAsync(string subjectId)
    {
        Task<Dictionary<string, List<ListedRawFile>>> task;
        lock (gate)
        {
            task = listing ??= Downsampler.ListRawFilesAsync(catalog, raw, spanStart, spanEnd, subjects, cancellationToken);
        }

        var files = await task.ConfigureAwait(false);
        return files.TryGetValue(subjectId, out var list) ? list : [];
    }

    /// <summary>Called once per marker of the span; the last call drops the listing.</summary>
    public void Release()
    {
        lock (gate)
        {
            if (--remaining == 0)
            {
                listing = null;
            }
        }
    }
}
