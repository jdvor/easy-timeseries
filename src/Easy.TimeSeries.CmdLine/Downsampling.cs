namespace Easy.TimeSeries.CmdLine;

using ConsoleAppFramework;
using Easy.TimeSeries.Aggregation;
using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;

internal static class Downsampling
{
    /// <summary>Aggregate raw files into one aggregated file per storage time span and subject.</summary>
    /// <param name="input">-i, Directory with raw files in the PathBuilder layout (yyyy/MM/dd/HH/{subject}.ets).</param>
    /// <param name="output">-o, Directory under which the aggregated root (e.g. agg-1m-daily) is created.</param>
    /// <param name="rawGranularity">Raw storage time span: hour, day, month or year.</param>
    /// <param name="aggregation">Aggregation time span: 1s, 10s, 1m, 5m, 15m, 1h or 1d.</param>
    /// <param name="storage">Aggregated storage time span: hour, day, month or year.</param>
    /// <param name="from">UTC start of the range, e.g. 2026-10-01; storage time spans overlapping the range are computed.</param>
    /// <param name="to">UTC end of the range, exclusive.</param>
    /// <param name="columns">Columns and functions: Voltage=avg+min+max+q99.9,Current,Status=last.</param>
    /// <param name="group">Category column splitting each raw file into several series.</param>
    /// <param name="subject">Only this subject; all subjects found in the range when omitted.</param>
    /// <param name="time">Raw time column; needed when raw files have several DateTime columns.</param>
    /// <param name="force">-f, Overwrite existing aggregated files.</param>
    public static async Task<int> DownsampleAsync(
        string input,
        string output,
        string rawGranularity,
        string aggregation,
        string storage,
        string from,
        string to,
        string columns,
        string? group = null,
        string? subject = null,
        string? time = null,
        bool force = false,
        CancellationToken ct = default)
    {
        var options = DownsampleArguments.Parse(
            input, output, rawGranularity, aggregation, storage, from, to, columns, group, subject, time, force);
        if (!options.Input.Exists)
        {
            throw new DirectoryNotFoundException($"Input directory '{options.Input.FullName}' does not exist.");
        }

        var summary = await RunAsync(options, ConsoleApp.Log, ct).ConfigureAwait(false);
        ConsoleApp.Log(
            $"{summary.Written} written, {summary.Empty} without rows, {summary.Skipped} skipped (exist), {summary.Failed} failed");
        return summary.Failed > 0 ? 1 : 0;
    }

    internal static async Task<DownsampleSummary> RunAsync(DownsampleOptions options, Action<string> log, CancellationToken ct)
    {
        var raw = new PathBuilder(options.RawGranularity);
        var source = new FileCatalog(options.Input);
        var target = new FileCatalog(options.Output);
        var aggregated = options.Spec.CreatePathBuilder();
        var downsampler = new Downsampler(options.Spec, options.RawGranularity);
        var summary = new DownsampleSummary();

        foreach (var spanStart in aggregated.GetBucketStarts(options.From, options.To))
        {
            var spanEnd = aggregated.GetBucketEnd(spanStart);
            var bySubject = new SortedDictionary<string, List<RawFile>>(StringComparer.Ordinal);
            var (_, prefix) = raw.GetExpectedPaths(spanStart, spanEnd);
            await foreach (var entry in source.ListAsync(prefix, ct).ConfigureAwait(false))
            {
                if (!raw.TryParse(entry.Path, out var bucketStart, out var subjectId)
                    || bucketStart < spanStart
                    || bucketStart >= spanEnd
                    || (options.Subject is not null && !string.Equals(subjectId, options.Subject, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!bySubject.TryGetValue(subjectId, out var files))
                {
                    bySubject[subjectId] = files = [];
                }

                files.Add(new RawFile(bucketStart, source.OpenRead(entry.Path), entry.Path));
            }

            foreach (var (subjectId, files) in bySubject)
            {
                var path = aggregated.GetFilePath(spanStart, subjectId);
                if (!options.Force && await target.GetAsync(path, ct).ConfigureAwait(false) is not null)
                {
                    log($"{path}: exists, skipped (pass --force to overwrite)");
                    ++summary.Skipped;
                    continue;
                }

                try
                {
                    await using var storage = target.OpenWrite(path);
                    var result = await downsampler.RunAsync(spanStart, files, storage, ct).ConfigureAwait(false);
                    if (result.Written)
                    {
                        log($"{path}: {result.Rows} rows, {result.Groups} groups, from {result.RawFiles} raw files ({result.RawRows} rows)");
                        ++summary.Written;
                    }
                    else
                    {
                        log($"{path}: no rows, not written");
                        ++summary.Empty;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log($"{path}: FAILED - {ex.Message}");
                    ++summary.Failed;
                }
            }
        }

        return summary;
    }
}

internal sealed class DownsampleSummary
{
    public int Written { get; set; }

    public int Empty { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }
}
