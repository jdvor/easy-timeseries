namespace Easy.Sample;

using Easy.TimeSeries;
using Easy.TimeSeries.Abstractions;

public sealed class PowerNodeReader : IReader<PowerNode>
{
    public Task<PowerNode[]> ReadAsync(
        ReadOptions options,
        IReadStorage storage,
        CancellationToken cancellationToken = default)
    {
        var materializer = new PowerNodeMaterializer();
        return Reader.ReadFromAsync(storage, materializer, options, cancellationToken);
    }
}
