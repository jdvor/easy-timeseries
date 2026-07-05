namespace Easy.Sample;

using Easy.TimeSeries;
using Easy.TimeSeries.Abstractions;

/// <summary>
/// Handwritten draft kept as a reference implementation for comparison with the source-generated
/// <c>PowerNodeReader</c>. Not used by the sample.
/// </summary>
public sealed class HandwrittenPowerNodeReader : IReader<PowerNode>
{
    public Task<PowerNode[]> ReadAsync(
        ReadOptions options,
        IReadStorage storage,
        CancellationToken cancellationToken = default)
    {
        var materializer = new HandwrittenPowerNodeMaterializer();
        return Reader.ReadFromAsync(storage, materializer, options, cancellationToken);
    }
}
