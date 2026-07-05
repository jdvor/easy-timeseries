// Demonstrates the source-generated PowerNodeWriter / PowerNodeReader end to end:
// DTO collection -> columnar bytes -> DTO collection.

using Easy.Sample;
using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Storage;

var start = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
var data = new List<PowerNode>();
for (var i = 0; i < 10; i++)
{
    data.Add(new PowerNode
    {
        CountryCode = i % 2 == 0 ? "CZ" : "DE",
        CapacityMw = 100.5f + i,
        Latitude = 50.08f + (i * 0.01f),
        Longitude = 14.43f + (i * 0.01f),
        PrimaryFuel = i % 3 == 0 ? "Coal" : "Gas",
        IsEnabled = i % 2 == 0,
        StartDuration = TimeSpan.FromMinutes(15 + i),
        StartPrice = 1200.50m + i,
        PowerNodeId = 9000 + i,
        MeasurementTimeUtc = start.AddMilliseconds(i * 750),
        CertifiedTimeUtc = start.AddDays(i - 30),
    });
}

using var storage = new InMemoryStorage();

await new PowerNodeWriter().WriteAsync(data, storage);
Console.WriteLine($"Written {data.Count} rows into {storage.Size} bytes.");

var readBack = await new PowerNodeReader().ReadAsync(new ReadOptions(), storage);
Console.WriteLine($"Read back {readBack.Length} rows:");
foreach (var node in readBack)
{
    Console.WriteLine(
        $"{node.PowerNodeId} {node.CountryCode} {node.PrimaryFuel,-4} capacity={node.CapacityMw,-7} " +
        $"lat={node.Latitude,-7} lon={node.Longitude,-7} enabled={node.IsEnabled,-5} " +
        $"start={node.StartDuration} price={node.StartPrice} " +
        $"measured={node.MeasurementTimeUtc:O} certified={node.CertifiedTimeUtc:yyyy-MM-dd}");
}
