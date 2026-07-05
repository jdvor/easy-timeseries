namespace Easy.Sample;

using Easy.TimeSeries;
using Easy.TimeSeries.Abstractions;
using TimeSeries.Storage;

/// <summary>
/// Handwritten draft kept as a reference implementation for comparison with the source-generated
/// <c>PowerNodeWriter</c>. Not used by the sample.
/// </summary>
public sealed class HandwrittenPowerNodeWriter : IWriter<PowerNode>
{
    /// <remarks>
    /// The methods of Writer must be called in the exact order of the column index defined in the ColumnAttribute.
    /// Also, such column indexes must start from 0 and be continuous.
    /// If the Label is not provided in the attribute, the property name will be used as column name.
    /// </remarks>
    public async Task WriteAsync(
        ICollection<PowerNode> data,
        IWriteStorage storage,
        CancellationToken cancellationToken = default)
    {
        var countries = data.Select(x => x.CountryCode);
        var capacities = data.Select(x => x.CapacityMw);
        var latitudes = data.Select(x => x.Latitude);
        var longitudes = data.Select(x => x.Longitude);
        var fuels = data.Select(x => x.PrimaryFuel);
        var enabled = data.Select(x => x.IsEnabled);
        var startDurations = data.Select(x => x.StartDuration);
        var startPrices = data.Select(x => x.StartPrice);
        var ids = data.Select(x => x.PowerNodeId);
        var measurementTimes = data.Select(x => x.MeasurementTimeUtc);
        var certifiedTimes = data.Select(x => x.CertifiedTimeUtc);

        var writer = new Writer(data.Count)
            .AddCategory(countries, "Country")
            .AddScaledNumber32(capacities, "Capacity (MW)", decimalPlaces: 3)
            .AddFloat(latitudes, "Latitude")
            .AddFloat(longitudes, "Longitude")
            .AddCategory(fuels, "Primary Fuel")
            .AddBool(enabled, "Enabled")
            .AddInterval(startDurations, "Start Duration", TimePrecision.Seconds)
            .AddDecimal(startPrices, "Start Price")
            .AddInt64(ids, "ID")
            .AddTimeOrdered(measurementTimes, "Measurement Time", TimePrecision.Milliseconds)
            .AddTimeUnordered(certifiedTimes, "Certified Time", TimePrecision.Days);

        await writer.WriteToAsync(storage, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAsync(
        ICollection<PowerNode> data,
        FileInfo file,
        CancellationToken cancellationToken = default)
    {
        using var fileStorage = new FileStorage(file);
        await WriteAsync(data, fileStorage, cancellationToken).ConfigureAwait(false);
        await fileStorage.CloseAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> WriteAsync(
        ICollection<PowerNode> data,
        CancellationToken cancellationToken = default)
    {
        using var memoryStorage = new InMemoryStorage();
        await WriteAsync(data, memoryStorage, cancellationToken);
        var bytes = memoryStorage.WrittenMemory.ToArray();
        await memoryStorage.CloseAsync(cancellationToken).ConfigureAwait(false);
        return bytes;
    }
}
