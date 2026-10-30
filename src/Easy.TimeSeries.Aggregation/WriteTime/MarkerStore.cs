namespace Easy.TimeSeries.Aggregation.WriteTime;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Paths;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Dirty markers: one small JSON file per aggregated file that must be (re)computed, at
/// <c>{marker root}/{aggregated root}/{date folders}/{subject}.json</c>; failed ones under
/// <c>{marker root}/failed/...</c>. Every write of a marker changes its version, which makes the processor's
/// conditional delete exact under concurrent raw writes.
/// </summary>
internal sealed class MarkerStore
{
    public const string FailedFolder = "failed";
    private const string Suffix = "json";

    private readonly IStorageCatalog catalog;
    private readonly AggregationConfiguration configuration;
    private readonly (AggregationSpec Spec, PathBuilder Active, PathBuilder Failed)[] layouts;

    public MarkerStore(IStorageCatalog catalog, AggregationConfiguration configuration)
    {
        this.catalog = catalog;
        this.configuration = configuration;
        layouts =
        [
            .. configuration.Specs.Select(spec => (
                spec,
                new PathBuilder($"{configuration.MarkerRoot}/{spec.Root}", spec.StorageTimeSpan, Suffix),
                new PathBuilder($"{configuration.MarkerRoot}/{FailedFolder}/{spec.Root}", spec.StorageTimeSpan, Suffix))),
        ];
    }

    public string PathOf(AggregationSpec spec, string subjectId, DateTime spanStart)
        => Layout(spec).Active.GetFilePath(spanStart, subjectId);

    public string FailedPathOf(AggregationSpec spec, string subjectId, DateTime spanStart)
        => Layout(spec).Failed.GetFilePath(spanStart, subjectId);

    /// <summary>Sets (or resets) the marker with a zero attempt count.</summary>
    public Task SetAsync(AggregationSpec spec, string subjectId, DateTime spanStart, CancellationToken cancellationToken)
        => WriteAsync(PathOf(spec, subjectId, spanStart), new MarkerContent(0, null), null, cancellationToken);

    /// <summary>Active markers, in no particular order.</summary>
    public async IAsyncEnumerable<Marker> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var entry in catalog.ListAsync($"{configuration.MarkerRoot}/", cancellationToken).ConfigureAwait(false))
        {
            foreach (var (spec, active, _) in layouts)
            {
                if (active.TryParse(entry.Path, out var spanStart, out var subjectId))
                {
                    yield return new Marker(spec, subjectId, spanStart, entry);
                    break;
                }
            }
        }
    }

    /// <summary>Failed markers, in no particular order.</summary>
    public async IAsyncEnumerable<Marker> ListFailedAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var prefix = $"{configuration.MarkerRoot}/{FailedFolder}/";
        await foreach (var entry in catalog.ListAsync(prefix, cancellationToken).ConfigureAwait(false))
        {
            foreach (var (spec, _, failed) in layouts)
            {
                if (failed.TryParse(entry.Path, out var spanStart, out var subjectId))
                {
                    yield return new Marker(spec, subjectId, spanStart, entry);
                    break;
                }
            }
        }
    }

    /// <summary>The marker's content; a missing or unreadable marker counts as a fresh one.</summary>
    public async Task<MarkerContent> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await catalog.OpenRead(path).ReadAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize(bytes.Span, MarkerJson.Default.MarkerContent) ?? new MarkerContent(0, null);
        }
#pragma warning disable CA1031 // a broken marker must still be processed
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            return new MarkerContent(0, null);
        }
    }

    /// <summary>Writes a marker; with <paramref name="ifMatch"/> only if it was not changed since.</summary>
    public async Task WriteAsync(string path, MarkerContent content, StorageVersion? ifMatch, CancellationToken cancellationToken)
    {
        var stamped = content with { UpdatedAt = configuration.TimeProvider.GetUtcNow() };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(stamped, MarkerJson.Default.MarkerContent);
        await using var storage = catalog.OpenWrite(path, ifMatch);
        await storage.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await storage.CloseAsync(cancellationToken).ConfigureAwait(false);
    }

    private (AggregationSpec Spec, PathBuilder Active, PathBuilder Failed) Layout(AggregationSpec spec)
    {
        foreach (var layout in layouts)
        {
            if (ReferenceEquals(layout.Spec, spec) || string.Equals(layout.Spec.Root, spec.Root, StringComparison.Ordinal))
            {
                return layout;
            }
        }

        throw new ArgumentException($"Spec '{spec.Root}' is not part of the configuration.", nameof(spec));
    }
}

/// <summary>A marker found in the catalog.</summary>
internal sealed record Marker(AggregationSpec Spec, string SubjectId, DateTime SpanStart, StorageEntry Entry);

/// <summary>Marker file content; the layout is internal and versioned by <see cref="FormatVersion"/>.</summary>
internal sealed record MarkerContent(int Attempts, string? LastError)
{
    public int FormatVersion { get; init; } = 1;

    public DateTimeOffset UpdatedAt { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MarkerContent))]
internal sealed partial class MarkerJson : JsonSerializerContext;
