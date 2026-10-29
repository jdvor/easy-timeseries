namespace Easy.TimeSeries.Aggregation;

/// <summary>
/// An unbound single-column aggregation, such as <see cref="Agg.Avg"/>. It does not know its input type yet; when the
/// spec is bound against raw headers, <see cref="Bind"/> turns it into a concrete <see cref="IAggregationFunction"/>
/// for one raw column.
/// </summary>
/// <remarks>
/// Descriptors are immutable; <see cref="As"/> and <see cref="WithOutput"/> return copies, so the shared
/// <see cref="Agg"/> instances are safe to reuse. Derive to add a custom single-column descriptor.
/// </remarks>
public abstract class AggregationDescriptor
{
    /// <summary>Explicit aggregated label, or <c>null</c> for the generated <c>{column}.{suffix}</c>.</summary>
    public string? Label { get; private set; }

    /// <summary>Explicit output encoding, or <c>null</c> for the function's default.</summary>
    public OutputEncoding? Output { get; private set; }

    /// <summary>Label suffix, e.g. <c>Avg</c> or <c>Q99.9</c>.</summary>
    protected abstract string Suffix { get; }

    /// <summary>Copy with an explicit aggregated label instead of the generated one.</summary>
    public AggregationDescriptor As(string label)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        var copy = (AggregationDescriptor)MemberwiseClone();
        copy.Label = label;
        return copy;
    }

    /// <summary>Copy with an explicit output encoding instead of the inherited one.</summary>
    public AggregationDescriptor WithOutput(OutputEncoding output)
    {
        var copy = (AggregationDescriptor)MemberwiseClone();
        copy.Output = output;
        return copy;
    }

    /// <summary>Creates the concrete function for one raw column.</summary>
    /// <exception cref="AggregationException">The function does not support <paramref name="inputType"/>.</exception>
    public IAggregationFunction Bind(string column, Type inputType)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        ArgumentNullException.ThrowIfNull(inputType);
        var resolved = Resolve(column, inputType);
        var label = resolved.Label ?? $"{column}.{resolved.Suffix}";
        return resolved.Create(column, inputType, label, resolved.Output);
    }

    /// <summary>Label this descriptor produces for <paramref name="column"/>, or <c>null</c> while it depends on the input type.</summary>
    internal string? LabelFor(string column) => Label ?? (IsTypeDependent ? null : $"{column}.{Suffix}");

    /// <summary>Whether the suffix is known only after the input type is (the default descriptor).</summary>
    internal virtual bool IsTypeDependent => false;

    /// <summary>The descriptor that actually binds to <paramref name="inputType"/>, carrying this one's label and output.</summary>
    internal virtual AggregationDescriptor Resolve(string column, Type inputType) => this;

    /// <summary>Creates the concrete function; throw <see cref="AggregationException"/> via <see cref="Unsupported"/> for an unsupported type.</summary>
    protected abstract IAggregationFunction Create(string column, Type inputType, string label, OutputEncoding? output);

    /// <summary>The error for an input type this descriptor cannot bind to.</summary>
    protected AggregationException Unsupported(string column, Type inputType)
        => new(
            $"{Suffix} does not support column '{column}' of type {inputType.Name}.",
            column: column,
            function: Label ?? $"{column}.{Suffix}");

    /// <summary>Copies label and output of this descriptor onto <paramref name="target"/>.</summary>
    internal AggregationDescriptor CarryOver(AggregationDescriptor target)
    {
        var copy = (AggregationDescriptor)target.MemberwiseClone();
        copy.Label = Label;
        copy.Output = Output;
        return copy;
    }
}
