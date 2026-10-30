namespace Easy.TimeSeries.Aggregation;

using Easy.TimeSeries.Paths;
using Easy.TimeSeries.Storage;
using System.Collections.Immutable;

/// <summary>
/// Declares one aggregation: the aggregation time span (raw time collapsed into one row), the storage time span (time
/// covered by one aggregated file), optional grouping, and the functions producing the aggregated columns.
/// </summary>
/// <remarks>
/// <para>
/// The spec is immutable: every fluent method returns a new spec, so a base spec can be shared and derived.
/// </para>
/// <para>
/// Output columns are time, group (if any), count, then one column per function in the order of <see cref="Column"/>
/// and <see cref="Function"/> calls. Only raw columns named in <see cref="Column"/> or read by a function are decoded;
/// the output layout depends on the spec alone.
/// </para>
/// </remarks>
public sealed class AggregationSpec
{
    private const string DefaultCountLabel = "Count";

    private readonly string? explicitRoot;
    private readonly ImmutableArray<SpecEntry> entries;

    /// <summary>Creates an empty spec; both time spans are required so neither is set by accident.</summary>
    /// <exception cref="ArgumentException">The aggregation time span does not fit into the storage time span.</exception>
    public AggregationSpec(AggregationTimeSpan aggregationTimeSpan, TimeGranularity storageTimeSpan)
        : this(aggregationTimeSpan, storageTimeSpan, null, null, null, DefaultCountLabel, [])
    {
    }

    private AggregationSpec(
        AggregationTimeSpan aggregationTimeSpan,
        TimeGranularity storageTimeSpan,
        string? explicitRoot,
        string? timeColumn,
        string? groupColumn,
        string countColumn,
        ImmutableArray<SpecEntry> entries)
    {
        ValidateTimeSpans(aggregationTimeSpan, storageTimeSpan);
        AggregationTimeSpan = aggregationTimeSpan;
        StorageTimeSpan = storageTimeSpan;
        this.explicitRoot = explicitRoot;
        TimeColumn = timeColumn;
        GroupColumn = groupColumn;
        CountColumn = countColumn;
        this.entries = entries;
        ValidateLabels();
    }

    /// <summary>How much raw time is collapsed into one aggregated row.</summary>
    public AggregationTimeSpan AggregationTimeSpan { get; }

    /// <summary>How much time one aggregated file covers.</summary>
    public TimeGranularity StorageTimeSpan { get; }

    /// <summary>Aggregated root; defaults to <c>agg-{slug}-{word}</c>, e.g. <c>agg-1m-daily</c>.</summary>
    public string Root => explicitRoot ?? $"agg-{AggregationTimeSpan.ToSlug()}-{StorageTimeSpan.ToWord()}";

    /// <summary>Raw time column label, or <c>null</c> to use the only DateTime column of each raw file.</summary>
    public string? TimeColumn { get; }

    /// <summary>Raw Category column splitting a raw file into several series, or <c>null</c>.</summary>
    public string? GroupColumn { get; }

    /// <summary>Label of the aggregated count column; <c>Count</c> by default.</summary>
    public string CountColumn { get; }

    internal ImmutableArray<SpecEntry> Entries => entries;

    /// <summary>Copy with both time spans replaced; validated as a pair, so no invalid intermediate state exists.</summary>
    public AggregationSpec WithTimeSpans(AggregationTimeSpan aggregationTimeSpan, TimeGranularity storageTimeSpan)
        => new(aggregationTimeSpan, storageTimeSpan, explicitRoot, TimeColumn, GroupColumn, CountColumn, entries);

    /// <summary>Copy with an explicit aggregated root instead of the one derived from the time spans.</summary>
    public AggregationSpec WithRoot(string root)
    {
        StoragePath.Validate(root, paramName: nameof(root));
        return new(AggregationTimeSpan, StorageTimeSpan, root, TimeColumn, GroupColumn, CountColumn, entries);
    }

    /// <summary>Names the raw time column; required when a raw file has more than one DateTime column.</summary>
    public AggregationSpec Time(string label)
        => new(AggregationTimeSpan, StorageTimeSpan, explicitRoot, CheckLabel(label), GroupColumn, CountColumn, entries);

    /// <summary>Groups rows by a raw Category column; one aggregated row per slice and group value.</summary>
    public AggregationSpec GroupBy(string label)
        => new(AggregationTimeSpan, StorageTimeSpan, explicitRoot, TimeColumn, CheckLabel(label), CountColumn, entries);

    /// <summary>Renames the count column, e.g. when a raw column is already named <c>Count</c>.</summary>
    public AggregationSpec CountLabel(string label)
        => new(AggregationTimeSpan, StorageTimeSpan, explicitRoot, TimeColumn, GroupColumn, CheckLabel(label), entries);

    /// <summary>
    /// Aggregates one raw column with predefined functions, e.g. <c>.Column("Voltage", Agg.Avg, Agg.Max)</c>.
    /// Without functions, numeric columns default to Avg, Bool to TrueRatio and Category to Last.
    /// </summary>
    public AggregationSpec Column(string label, params ReadOnlySpan<AggregationDescriptor> functions)
    {
        CheckLabel(label);
        foreach (var entry in entries)
        {
            if (entry is ColumnEntry c && string.Equals(c.Label, label, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Column '{label}' is already in the spec; name all its functions in one call.", nameof(label));
            }
        }

        var descriptors = functions.IsEmpty ? [Agg.Default] : ImmutableArray.Create(functions);
        foreach (var descriptor in descriptors)
        {
            ArgumentNullException.ThrowIfNull(descriptor, nameof(functions));
        }

        return With(entries.Add(new ColumnEntry(label, descriptors)));
    }

    /// <summary>Adds a custom function, typically one reading several raw columns.</summary>
    public AggregationSpec Function(IAggregationFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        CheckLabel(function.Name);
        if (function.Inputs is not { Count: > 0 })
        {
            throw new ArgumentException($"Function '{function.Name}' declares no inputs.", nameof(function));
        }

        if (!ClrTypes.IsSupported(function.ResultType))
        {
            throw new ArgumentException(
                $"Function '{function.Name}' returns {function.ResultType.Name}; use one of: {ClrTypes.SupportedNames}.",
                nameof(function));
        }

        if (function.Output.IsInherited && function.Inputs.Count != 1)
        {
            throw new ArgumentException(
                $"Function '{function.Name}' reads {function.Inputs.Count} columns and must declare a concrete output encoding.",
                nameof(function));
        }

        if (!function.Output.IsInherited && !ClrTypes.IsCompatible(function.ResultType, function.Output.ValueType))
        {
            throw new ArgumentException(
                $"Function '{function.Name}' returns {function.ResultType.Name}, which cannot be stored as {function.Output}.",
                nameof(function));
        }

        return With(entries.Add(new FunctionEntry(function)));
    }

    /// <summary>Path builder for the aggregated files of this spec.</summary>
    public PathBuilder CreatePathBuilder() => new(Root, StorageTimeSpan);

    /// <summary>
    /// Output columns this spec produces over the given raw headers, in file order. Binds the spec without decoding
    /// data; meant for tests asserting that a reader DTO matches the aggregated layout.
    /// </summary>
    /// <exception cref="AggregationException">A raw header does not match the spec.</exception>
    public IReadOnlyList<AggregatedColumn> Describe(IReadOnlyList<Header> rawHeaders)
    {
        ArgumentNullException.ThrowIfNull(rawHeaders);
        if (rawHeaders.Count == 0)
        {
            throw new ArgumentException("At least one raw header is required.", nameof(rawHeaders));
        }

        var binding = new SpecBinding(this);
        foreach (var header in rawHeaders)
        {
            binding.Bind(header, rawName: null);
        }

        return binding.Describe();
    }

    /// <summary>Number of function columns, known without raw headers.</summary>
    internal int FunctionCount => entries.Sum(x => x is ColumnEntry c ? c.Descriptors.Length : 1);

    private static void ValidateTimeSpans(AggregationTimeSpan aggregationTimeSpan, TimeGranularity storageTimeSpan)
    {
        if (!aggregationTimeSpan.IsDefined())
        {
            throw new ArgumentOutOfRangeException(nameof(aggregationTimeSpan), aggregationTimeSpan, "Unknown aggregation time span.");
        }

        if (storageTimeSpan is < TimeGranularity.Hour or > TimeGranularity.Year)
        {
            throw new ArgumentOutOfRangeException(nameof(storageTimeSpan), storageTimeSpan, "Unknown storage time span.");
        }

        if (aggregationTimeSpan.ToTimeSpan() > storageTimeSpan.MinLength())
        {
            throw new ArgumentException(
                $"Aggregation time span {aggregationTimeSpan} does not fit into storage time span {storageTimeSpan}.",
                nameof(aggregationTimeSpan));
        }
    }

    private static string CheckLabel(string label)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        if (label.Length > ColumnInfo.MaxLabelLength)
        {
            throw new ArgumentException(
                $"Label '{label}' exceeds {ColumnInfo.MaxLabelLength} characters; set a shorter one explicitly.",
                nameof(label));
        }

        return label;
    }

    private AggregationSpec With(ImmutableArray<SpecEntry> newEntries)
        => new(AggregationTimeSpan, StorageTimeSpan, explicitRoot, TimeColumn, GroupColumn, CountColumn, newEntries);

    /// <summary>Checks the labels known without raw headers: uniqueness, length and the column limit.</summary>
    private void ValidateLabels()
    {
        var columns = 1 + (GroupColumn is null ? 0 : 1) + 1 + FunctionCount;
        if (columns > Header.MaxColumns)
        {
            throw new ArgumentException($"The spec produces {columns} columns; a file holds at most {Header.MaxColumns}.");
        }

        var labels = new HashSet<string>(StringComparer.Ordinal);
        Add(TimeColumn);
        Add(GroupColumn);
        Add(CountColumn);
        foreach (var entry in entries)
        {
            if (entry is FunctionEntry f)
            {
                Add(f.Function.Name);
                continue;
            }

            var column = (ColumnEntry)entry;
            foreach (var descriptor in column.Descriptors)
            {
                var label = descriptor.LabelFor(column.Label);
                if (label is not null)
                {
                    CheckLabel(label);
                }

                Add(label);
            }
        }

        void Add(string? label)
        {
            if (label is not null && !labels.Add(label))
            {
                throw new ArgumentException($"Aggregated label '{label}' is used twice; set an explicit label with As(...).");
            }
        }
    }
}

/// <summary>One aggregated column as produced by a spec over given raw headers.</summary>
/// <param name="Index">Column index in the aggregated file.</param>
/// <param name="Label">Column label.</param>
/// <param name="ValueType">On-disk encoding.</param>
/// <param name="Meta">Decimal places or time precision, 0 otherwise.</param>
/// <param name="ClrType">Type the column decodes to.</param>
public readonly record struct AggregatedColumn(int Index, string Label, ColumnValueType ValueType, int Meta, Type ClrType);

internal abstract record SpecEntry;

internal sealed record ColumnEntry(string Label, ImmutableArray<AggregationDescriptor> Descriptors) : SpecEntry;

internal sealed record FunctionEntry(IAggregationFunction Function) : SpecEntry;
