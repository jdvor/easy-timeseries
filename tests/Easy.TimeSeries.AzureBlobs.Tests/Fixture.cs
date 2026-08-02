namespace Easy.TimeSeries.AzureBlobs.Tests;

using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Paths;
using Testcontainers.Azurite;
using Xunit.Sdk;

public class Fixture : IAsyncLifetime
{
    public const string HourContainerName = "hour";
    public const string DayContainerName = "day";
    public const string MonthContainerName = "month";
    public const string YearContainerName = "year";

    private readonly AzuriteContainer azurite;
    private readonly ILogger<Fixture> logger;

    private string? connectionString;
    private AzureBlobStorageFactory? hourFactory;
    private AzureBlobStorageFactory? dayFactory;
    private AzureBlobStorageFactory? monthFactory;
    private AzureBlobStorageFactory? yearFactory;
    private bool disposed;

    public AzureBlobStorageFactory HourFactory
    {
        get
        {
            if (hourFactory is null)
            {
                ArgumentNullException.ThrowIfNull(connectionString);
                hourFactory = new AzureBlobStorageFactory(connectionString, HourContainerName, TimeGranularity.Hour);
            }

            return hourFactory;
        }
    }

    public AzureBlobStorageFactory DayFactory
    {
        get
        {
            if (dayFactory is null)
            {
                ArgumentNullException.ThrowIfNull(connectionString);
                dayFactory = new AzureBlobStorageFactory(connectionString, DayContainerName, TimeGranularity.Day);
            }

            return dayFactory;
        }
    }

    public AzureBlobStorageFactory MonthFactory
    {
        get
        {
            if (monthFactory is null)
            {
                ArgumentNullException.ThrowIfNull(connectionString);
                monthFactory = new AzureBlobStorageFactory(connectionString, MonthContainerName, TimeGranularity.Month);
            }

            return monthFactory;
        }
    }

    public AzureBlobStorageFactory YearFactory
    {
        get
        {
            if (yearFactory is null)
            {
                ArgumentNullException.ThrowIfNull(connectionString);
                yearFactory = new AzureBlobStorageFactory(connectionString, YearContainerName, TimeGranularity.Year);
            }

            return yearFactory;
        }
    }

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
        string[] allContainers = [HourContainerName, DayContainerName, MonthContainerName, YearContainerName];
        foreach (var container in allContainers)
        {
            var containerClient = new BlobContainerClient(connectionString, container);
            await containerClient.CreateIfNotExistsAsync();
        }

        logger.LogInformation("Azurite started");
    }
}
