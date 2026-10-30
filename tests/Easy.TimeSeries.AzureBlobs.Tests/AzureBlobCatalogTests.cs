namespace Easy.TimeSeries.AzureBlobs.Tests;

using Azure;
using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Tests;

/// <summary>
/// Runs the shared catalog contract against Azurite. Conditional writes and deletes here are ETag preconditions
/// checked by the service, so these cases also pin the If-Match semantics the catalog relies on.
/// </summary>
[Collection(nameof(FixtureCollection))]
public sealed class AzureBlobCatalogTests : StorageCatalogContract
{
    public AzureBlobCatalogTests(Fixture fixture)
    {
        Catalog = new AzureBlobCatalog(fixture.CreateContainer());
    }

    protected override IStorageCatalog Catalog { get; }

    [Fact]
    public async Task Reading_a_missing_path_throws_not_found()
    {
        var ex = await Assert.ThrowsAsync<RequestFailedException>(() => Catalog.OpenRead("a/missing.ets").ReadAsync(Ct));
        Assert.Equal(404, ex.Status);
    }

    [Fact]
    public async Task Conditional_write_to_a_missing_path_throws()
    {
        await Assert.ThrowsAsync<StorageVersionMismatchException>(
            () => WriteAsync("a/never.ets", [1], new StorageVersion("\"0x8D0000000000000\"")));
        Assert.Null(await Catalog.GetAsync("a/never.ets", Ct));
    }
}
