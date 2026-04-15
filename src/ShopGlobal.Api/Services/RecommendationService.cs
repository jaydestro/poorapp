using Microsoft.Azure.Cosmos;
using ShopGlobal.Api.Models;

namespace ShopGlobal.Api.Services;

public class RecommendationService
{
    private readonly CosmosService _cosmosService;

    public RecommendationService(CosmosService cosmosService)
    {
        _cosmosService = cosmosService;
    }

    public async Task<List<RecommendedProduct>> GetRecommendationsAsync(string productId)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // Intentionally loading ALL recommendation documents and filtering client-side
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'recommendation'");

            var iterator = container.GetItemQueryIterator<Recommendation>(query);
            var allRecommendations = new List<Recommendation>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                allRecommendations.AddRange(response);
            }

            // Filter in memory
            var match = allRecommendations.FirstOrDefault(r => r.SourceProductId == productId);
            return match?.RecommendedProducts ?? [];
        });
    }

    // Intentionally rebuilding ALL recommendations on every call
    public async Task RebuildRecommendationsAsync()
    {
        var container = _cosmosService.GetContainer();

        // Step 1: Load ALL orders into memory
        var orderQuery = new QueryDefinition("SELECT * FROM c WHERE c.type = 'order'");
        var orderIterator = container.GetItemQueryIterator<Order>(orderQuery);
        var allOrders = new List<Order>();

        while (orderIterator.HasMoreResults)
        {
            var response = await orderIterator.ReadNextAsync();
            allOrders.AddRange(response);
        }

        // Step 2: Build co-purchase matrix in memory
        var coPurchases = new Dictionary<string, Dictionary<string, int>>();

        foreach (var order in allOrders)
        {
            var productIds = order.LineItems.Select(li => li.ProductId).Distinct().ToList();

            foreach (var pid1 in productIds)
            {
                if (!coPurchases.ContainsKey(pid1))
                    coPurchases[pid1] = new Dictionary<string, int>();

                foreach (var pid2 in productIds.Where(p => p != pid1))
                {
                    if (coPurchases[pid1].ContainsKey(pid2))
                        coPurchases[pid1][pid2]++;
                    else
                        coPurchases[pid1][pid2] = 1;
                }
            }
        }

        // Step 3: Load all products for names
        var productQuery = new QueryDefinition("SELECT * FROM c WHERE c.type = 'product'");
        var productIterator = container.GetItemQueryIterator<Product>(productQuery);
        var products = new Dictionary<string, Product>();

        while (productIterator.HasMoreResults)
        {
            var response = await productIterator.ReadNextAsync();
            foreach (var p in response)
                products[p.Id] = p;
        }

        // Step 4: Delete existing recommendations
        var deleteQuery = new QueryDefinition("SELECT * FROM c WHERE c.type = 'recommendation'");
        var deleteIterator = container.GetItemQueryIterator<Recommendation>(deleteQuery);

        while (deleteIterator.HasMoreResults)
        {
            var response = await deleteIterator.ReadNextAsync();
            foreach (var rec in response)
            {
                // Intentionally deleting one at a time
                await container.DeleteItemAsync<Recommendation>(rec.Id, new PartitionKey("recommendation"));
            }
        }

        // Step 5: Write new recommendations one at a time
        foreach (var entry in coPurchases)
        {
            var sourceProduct = products.GetValueOrDefault(entry.Key);

            var recommendation = new Recommendation
            {
                SourceProductId = entry.Key,
                SourceProductName = sourceProduct?.Name ?? "Unknown",
                RecommendedProducts = entry.Value
                    .OrderByDescending(kv => kv.Value)
                    .Take(10)
                    .Select(kv => new RecommendedProduct
                    {
                        ProductId = kv.Key,
                        ProductName = products.GetValueOrDefault(kv.Key)?.Name ?? "Unknown",
                        Score = kv.Value,
                        CoPurchaseCount = kv.Value
                    })
                    .ToList(),
                PurchaseHistory = allOrders
                    .Where(o => o.LineItems.Any(li => li.ProductId == entry.Key))
                    .Select(o => new PurchaseHistoryEntry
                    {
                        OrderId = o.Id,
                        CustomerId = o.CustomerId,
                        PurchaseDate = o.OrderDate,
                        CoProducts = o.LineItems.Select(li => li.ProductId).ToList()
                    })
                    .ToList(),
                LastUpdated = DateTime.UtcNow
            };

            // Intentionally inserting one at a time, no bulk
            await container.CreateItemAsync(recommendation, new PartitionKey("recommendation"));
        }
    }
}
