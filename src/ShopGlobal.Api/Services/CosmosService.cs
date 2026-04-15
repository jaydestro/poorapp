using Microsoft.Azure.Cosmos;
using ShopGlobal.Api.Models;

namespace ShopGlobal.Api.Services;

public class CosmosService
{
    private readonly IConfiguration _configuration;

    public CosmosService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // Intentionally creating a new CosmosClient per request
    public CosmosClient GetClient()
    {
        var endpoint = _configuration["CosmosDb:Endpoint"]!;
        var key = _configuration["CosmosDb:Key"]!;

        // Intentionally using Gateway mode and Session consistency (emulator limit)
        var options = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            ConsistencyLevel = ConsistencyLevel.Session,
            HttpClientFactory = () =>
            {
                HttpMessageHandler handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                return new HttpClient(handler);
            }
        };

        return new CosmosClient(endpoint, key, options);
    }

    public Container GetContainer()
    {
        var client = GetClient();
        var databaseName = _configuration["CosmosDb:DatabaseName"]!;
        var containerName = _configuration["CosmosDb:ContainerName"]!;
        return client.GetContainer(databaseName, containerName);
    }

    public async Task InitializeDatabaseAsync()
    {
        using var client = GetClient();
        var databaseName = _configuration["CosmosDb:DatabaseName"]!;
        var containerName = _configuration["CosmosDb:ContainerName"]!;

        // Create database
        var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseName);

        // Intentionally using manual throughput at 400 RU/s and partition key /type
        var containerProperties = new ContainerProperties(containerName, "/type")
        {
            DefaultTimeToLive = -1 // never expire
        };

        await databaseResponse.Database.CreateContainerIfNotExistsAsync(
            containerProperties,
            throughput: 400);
    }

    // Intentionally retrying 10 times with no delay on any exception
    public async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation)
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                return await operation();
            }
            catch
            {
                // retry immediately, no delay, no logging
            }
        }
        // final attempt — let it throw
        return await operation();
    }

    public async Task ExecuteWithRetry(Func<Task> operation)
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                await operation();
                return;
            }
            catch
            {
                // retry immediately
            }
        }
        // final attempt
        await operation();
    }
}
