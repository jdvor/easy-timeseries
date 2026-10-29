namespace Easy.TimeSeries.AzureBlobs.Tests;

using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Testcontainers.Azurite;
using Xunit.Sdk;

public class Fixture : IAsyncLifetime
{
    private readonly AzuriteContainer azurite;
    private readonly ILogger<Fixture> logger;

    private string? connectionString;
    private bool disposed;

    public Fixture(IMessageSink messageSink)
    {
        logger = messageSink.ToLogger<Fixture>();

        // The Azure.Storage.Blobs SDK negotiates a newer REST API version than any published Azurite image
        // supports, so relax Azurite's version check to let the client's requests through.
        azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.36.0")
            .WithCommand("--skipApiVersionCheck")
            .WithLogger(logger)
            .WithCleanUp(true)
            .Build();
    }

    public string ConnectionString => connectionString ?? throw new InvalidOperationException("Azurite is not started.");

    /// <summary>A new, empty container, so tests never see each other's blobs.</summary>
    public BlobContainerClient CreateContainer()
    {
        var container = new BlobContainerClient(ConnectionString, $"c{Guid.NewGuid():N}");
        container.CreateIfNotExists();
        return container;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await azurite.DisposeAsync();

        disposed = true;
    }

    public async ValueTask InitializeAsync()
    {
        logger.LogInformation("Starting azurite");
        await azurite.StartAsync();
        connectionString = azurite.GetConnectionString();
        logger.LogInformation("Azurite started");
    }
}
